using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Nona.Libsql;

public sealed class LibsqlMirroredLocalDatabaseClient : ILibsqlDatabaseClient, IDisposable
{
    private const string RowIdAlias = "__nona_rowid__";

    private readonly LibsqlHttpDatabaseClient _upstreamClient;
    private readonly LibsqlSqliteDatabase _replicaDatabase;
    private readonly LocalSqliteDatabaseClient _localClient;
    private readonly string _replicaPath;
    private readonly string _localReplicaRole;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _migrationsFolder;
    private readonly List<ReplicaTableDefinition> _tables = [];

    private bool _initialized;

    public LibsqlMirroredLocalDatabaseClient(
        LibsqlHttpDatabaseClient upstreamClient,
        IOptions<LibsqlOptions> options)
    {
        _upstreamClient = upstreamClient;

        _replicaPath = ResolveReplicaPath(options.Value.LocalReplicaPath);
        _localReplicaRole = options.Value.LocalReplicaRole;
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _replicaPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();

        _replicaDatabase = new LibsqlSqliteDatabase(connectionString);
        _localClient = new LocalSqliteDatabaseClient(_replicaDatabase);
        _migrationsFolder = ResolveMigrationsFolder();
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        var hadExistingReplicaFile = HasExistingReplicaFile();

        await _gate.WaitAsync(ct);
        try
        {
            await EnsureInitializedCoreAsync(ct);
            if (IsReplicaRole())
            {
                await SyncFromUpstreamIfAvailableAsync(hadExistingReplicaFile, ct);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<LibsqlQueryResult> ExecuteAsync(string sql, object? parameters = null, CancellationToken ct = default)
    {
        var results = await ExecuteBatchAsync([new LibsqlStatement(sql, parameters)], ct);
        return results[0];
    }

    public async Task<IReadOnlyList<LibsqlQueryResult>> ExecuteBatchAsync(
        IEnumerable<LibsqlStatement> statements,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(statements);

        var batch = statements.ToList();
        if (batch.Count == 0)
        {
            return [];
        }

        await _gate.WaitAsync(ct);
        try
        {
            await EnsureInitializedCoreAsync(ct);

            if (IsPrimaryRole())
            {
                return await ExecuteLocalBatchCoreAsync(batch, ct);
            }

            if (batch.All(statement => IsReadOnlyStatement(statement.Sql)))
            {
                await SyncFromUpstreamIfAvailableAsync(allowCachedReplicaFallback: true, ct);
                return await ExecuteLocalBatchCoreAsync(batch, ct);
            }

            var remoteResults = await _upstreamClient.ExecuteBatchAsync(batch, ct);
            await SyncFromUpstreamCoreAsync(ct);
            return remoteResults;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        _gate.Dispose();
        _replicaDatabase.Dispose();
    }

    public async Task<IReadOnlyList<LibsqlQueryResult>> ExecuteLocalBatchAsync(
        IEnumerable<LibsqlStatement> statements,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(statements);

        var batch = statements.ToList();
        if (batch.Count == 0)
        {
            return [];
        }

        await _gate.WaitAsync(ct);
        try
        {
            await EnsureInitializedCoreAsync(ct);
            return await ExecuteLocalBatchCoreAsync(batch, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task EnsureInitializedCoreAsync(CancellationToken ct)
    {
        if (_initialized)
        {
            return;
        }

        var replicaDirectory = Path.GetDirectoryName(_replicaPath);
        if (!string.IsNullOrWhiteSpace(replicaDirectory))
        {
            Directory.CreateDirectory(replicaDirectory);
        }

        await _replicaDatabase.InitializeDatabaseAsync(_migrationsFolder, ct);
        _tables.Clear();
        _tables.AddRange(await LoadReplicaTableDefinitionsAsync(ct));
        _initialized = true;
    }

    private async Task<IReadOnlyList<LibsqlQueryResult>> ExecuteLocalBatchCoreAsync(
        IReadOnlyList<LibsqlStatement> batch,
        CancellationToken ct)
    {
        return await _localClient.ExecuteBatchAsync(batch, ct);
    }

    private async Task SyncFromUpstreamCoreAsync(CancellationToken ct)
    {
        if (_tables.Count == 0)
        {
            return;
        }

        var statements = _tables
            .Select(table => new LibsqlStatement(BuildSnapshotSelectSql(table.Name, table.Columns)))
            .ToList();

        var remoteResults = await _upstreamClient.ExecuteBatchAsync(statements, ct);
        if (remoteResults.Count != _tables.Count)
        {
            throw new LibsqlException("libSQL local replica synchronization returned an unexpected result count.");
        }

        var connection = await _replicaDatabase.GetConnectionAsync(ct);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(ct);

        try
        {
            foreach (var pair in _tables.Zip(remoteResults, (table, result) => (table, result)))
            {
                await ReplaceLocalTableContentsAsync(connection, transaction, pair.table, pair.result, ct);
            }

            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    private async Task SyncFromUpstreamIfAvailableAsync(bool allowCachedReplicaFallback, CancellationToken ct)
    {
        try
        {
            await SyncFromUpstreamCoreAsync(ct);
        }
        catch (LibsqlException) when (allowCachedReplicaFallback)
        {
            // Keep serving the last committed local snapshot when the primary is temporarily unavailable.
        }
    }

    private async Task<IReadOnlyList<ReplicaTableDefinition>> LoadReplicaTableDefinitionsAsync(CancellationToken ct)
    {
        var connection = await _replicaDatabase.GetConnectionAsync(ct);
        var tables = new List<ReplicaTableDefinition>();

        using var tableCommand = connection.CreateCommand();
        tableCommand.CommandText =
            """
            SELECT name
            FROM sqlite_master
            WHERE type = 'table'
              AND name NOT LIKE 'sqlite_%'
            ORDER BY name
            """;

        using var tableReader = await tableCommand.ExecuteReaderAsync(ct);
        while (await tableReader.ReadAsync(ct))
        {
            var tableName = tableReader.GetString(0);
            var columns = await LoadTableColumnsAsync(connection, tableName, ct);
            tables.Add(new ReplicaTableDefinition(tableName, columns));
        }

        return tables;
    }

    private static async Task<IReadOnlyList<string>> LoadTableColumnsAsync(
        SqliteConnection connection,
        string tableName,
        CancellationToken ct)
    {
        var columns = new List<string>();

        using var columnCommand = connection.CreateCommand();
        columnCommand.CommandText = $"PRAGMA table_info({LibsqlSqliteCommandExecutor.QuoteIdentifier(tableName)})";

        using var columnReader = await columnCommand.ExecuteReaderAsync(ct);
        while (await columnReader.ReadAsync(ct))
        {
            columns.Add(columnReader.GetString(columnReader.GetOrdinal("name")));
        }

        return columns;
    }

    private static async Task ReplaceLocalTableContentsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        ReplicaTableDefinition table,
        LibsqlQueryResult result,
        CancellationToken ct)
    {
        using (var deleteCommand = connection.CreateCommand())
        {
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandText = $"DELETE FROM {LibsqlSqliteCommandExecutor.QuoteIdentifier(table.Name)}";
            await deleteCommand.ExecuteNonQueryAsync(ct);
        }

        if (result.Rows.Count == 0)
        {
            return;
        }

        var insertColumns = new[] { "rowid" }.Concat(table.Columns).ToList();
        var parameterNames = Enumerable.Range(0, insertColumns.Count)
            .Select(index => $"@p{index}")
            .ToList();

        var insertSql =
            $"INSERT INTO {LibsqlSqliteCommandExecutor.QuoteIdentifier(table.Name)} " +
            $"({string.Join(", ", insertColumns.Select(LibsqlSqliteCommandExecutor.QuoteIdentifier))}) " +
            $"VALUES ({string.Join(", ", parameterNames)})";

        foreach (var row in result.Rows)
        {
            using var insertCommand = connection.CreateCommand();
            insertCommand.Transaction = transaction;
            insertCommand.CommandText = insertSql;

            LibsqlSqliteCommandExecutor.AddParameter(insertCommand, parameterNames[0], row.GetInt64(RowIdAlias));

            for (var index = 0; index < table.Columns.Count; index++)
            {
                LibsqlSqliteCommandExecutor.AddParameter(
                    insertCommand,
                    parameterNames[index + 1],
                    row.GetValue(table.Columns[index]));
            }

            await insertCommand.ExecuteNonQueryAsync(ct);
        }
    }

    private static string BuildSnapshotSelectSql(string tableName, IReadOnlyList<string> columns)
    {
        var selectColumns = string.Join(
            ", ",
            columns.Select(LibsqlSqliteCommandExecutor.QuoteIdentifier));

        return
            $"SELECT rowid AS {LibsqlSqliteCommandExecutor.QuoteIdentifier(RowIdAlias)}, {selectColumns} " +
            $"FROM {LibsqlSqliteCommandExecutor.QuoteIdentifier(tableName)}";
    }

    private static bool IsReadOnlyStatement(string sql)
    {
        var trimmed = sql.TrimStart();
        return trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("PRAGMA", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("WITH", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveReplicaPath(string configuredPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configuredPath);
        return Path.GetFullPath(configuredPath);
    }

    private bool HasExistingReplicaFile()
    {
        return File.Exists(_replicaPath) && new FileInfo(_replicaPath).Length > 0;
    }

    private bool IsPrimaryRole()
    {
        return _localReplicaRole.Equals("Primary", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsReplicaRole()
    {
        return _localReplicaRole.Equals("Replica", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveMigrationsFolder()
    {
        var basePath = AppDomain.CurrentDomain.BaseDirectory;
        var outputFolder = Path.Combine(basePath, "Migrations");
        if (Directory.Exists(outputFolder))
        {
            return outputFolder;
        }

        return Path.GetFullPath(Path.Combine(
            Directory.GetCurrentDirectory(),
            "..",
            "Infrastructure",
            "Migrations"));
    }

    private sealed record ReplicaTableDefinition(string Name, IReadOnlyList<string> Columns);
}
