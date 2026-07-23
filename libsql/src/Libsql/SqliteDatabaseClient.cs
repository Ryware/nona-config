using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using System.Data.Common;

namespace Nona.Libsql;

public sealed class SqliteDatabaseClient : ILibsqlDatabaseClient, IDisposable
{
    private readonly string _connectionString;
    private readonly int _commandTimeoutSeconds;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _initialized;
    private bool _disposed;

    public SqliteDatabaseClient(IOptions<SqliteOptions> options)
        : this(options.Value)
    {
    }

    public SqliteDatabaseClient(string dataSource, int commandTimeoutSeconds = 30)
        : this(new SqliteOptions
        {
            DataSource = dataSource,
            TimeoutSeconds = commandTimeoutSeconds
        })
    {
    }

    private SqliteDatabaseClient(SqliteOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.DataSource);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.TimeoutSeconds);

        var dataSource = options.DataSource.Equals(":memory:", StringComparison.Ordinal)
            ? options.DataSource
            : Path.GetFullPath(options.DataSource);

        _commandTimeoutSeconds = options.TimeoutSeconds;
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dataSource,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Default,
            Pooling = true,
            DefaultTimeout = options.TimeoutSeconds
        }.ToString();
    }

    public async Task<LibsqlQueryResult> ExecuteAsync(
        string sql,
        object? parameters = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        ObjectDisposedException.ThrowIf(_disposed, this);

        await EnsureInitializedAsync(ct);

        try
        {
            await using var connection = await OpenConnectionAsync(ct);
            return await ExecuteStatementAsync(connection, transaction: null, new LibsqlStatement(sql, parameters), ct);
        }
        catch (SqliteException ex)
        {
            throw new LibsqlException("SQLite statement execution failed.", ex);
        }
    }

    public async Task<IReadOnlyList<LibsqlQueryResult>> ExecuteBatchAsync(
        IEnumerable<LibsqlStatement> statements,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(statements);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var batch = statements.ToList();
        if (batch.Count == 0)
        {
            return [];
        }

        await EnsureInitializedAsync(ct);

        try
        {
            await using var connection = await OpenConnectionAsync(ct);
            await using var transaction = connection.BeginTransaction(deferred: false);
            var results = new List<LibsqlQueryResult>(batch.Count);

            try
            {
                foreach (var statement in batch)
                {
                    ct.ThrowIfCancellationRequested();
                    results.Add(await ExecuteStatementAsync(connection, transaction, statement, ct));
                }

                await transaction.CommitAsync(ct);
                return results;
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }
        }
        catch (SqliteException ex)
        {
            throw new LibsqlException("SQLite batch execution failed.", ex);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _initializationLock.Dispose();
    }

    private async Task EnsureInitializedAsync(CancellationToken ct)
    {
        if (_initialized)
        {
            return;
        }

        await _initializationLock.WaitAsync(ct);
        try
        {
            if (_initialized)
            {
                return;
            }

            var builder = new SqliteConnectionStringBuilder(_connectionString);
            if (!builder.DataSource.Equals(":memory:", StringComparison.Ordinal))
            {
                var directory = Path.GetDirectoryName(builder.DataSource);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }
            }

            await using var connection = await OpenConnectionAsync(ct);
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA journal_mode=WAL";
            command.CommandTimeout = _commandTimeoutSeconds;
            _ = await command.ExecuteScalarAsync(ct);

            _initialized = true;
        }
        catch (SqliteException ex)
        {
            throw new LibsqlException("SQLite initialization failed while enabling WAL mode.", ex);
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken ct)
    {
        var connection = new SqliteConnection(_connectionString);
        try
        {
            await connection.OpenAsync(ct);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private async Task<LibsqlQueryResult> ExecuteStatementAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        LibsqlStatement statement,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(statement.Sql);

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandTimeout = _commandTimeoutSeconds;
        command.CommandText = LibsqlCommandHelpers.BindParameters(
            command,
            statement.Sql,
            statement.Parameters);

        IReadOnlyList<LibsqlRow> rows = [];
        int affectedRowCount;

        if (LibsqlCommandHelpers.ReturnsRows(statement.Sql))
        {
            await using var reader = await command.ExecuteReaderAsync(ct);
            rows = await ReadRowsAsync(reader, ct);
            affectedRowCount = reader.RecordsAffected >= 0
                ? reader.RecordsAffected
                : LibsqlCommandHelpers.IsQuery(statement.Sql) ? 0 : rows.Count;
        }
        else
        {
            affectedRowCount = await command.ExecuteNonQueryAsync(ct);
        }

        long? lastInsertRowId = null;
        if (LibsqlCommandHelpers.IsInsertStatement(statement.Sql)
            && (affectedRowCount > 0 || rows.Count > 0))
        {
            await using var idCommand = connection.CreateCommand();
            idCommand.Transaction = transaction;
            idCommand.CommandTimeout = _commandTimeoutSeconds;
            idCommand.CommandText = "SELECT last_insert_rowid()";
            lastInsertRowId = Convert.ToInt64(await idCommand.ExecuteScalarAsync(ct));
        }

        return new LibsqlQueryResult(rows, affectedRowCount, lastInsertRowId);
    }

    private static async Task<IReadOnlyList<LibsqlRow>> ReadRowsAsync(
        DbDataReader reader,
        CancellationToken ct)
    {
        var columns = Enumerable.Range(0, reader.FieldCount)
            .Select(reader.GetName)
            .ToArray();
        var rows = new List<LibsqlRow>();

        while (await reader.ReadAsync(ct))
        {
            var values = new Dictionary<string, object?>(columns.Length, StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < columns.Length; index++)
            {
                values[columns[index]] = await reader.IsDBNullAsync(index, ct)
                    ? null
                    : reader.GetValue(index);
            }

            rows.Add(new LibsqlRow(columns, values));
        }

        return rows;
    }
}
