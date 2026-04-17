using Nelknet.LibSQL.Data;
using System.Data;
using System.Data.Common;
using System.Globalization;

namespace Nona.Libsql;

public sealed class NelknetLibsqlDatabaseClient : ILibsqlDatabaseClient, IDisposable
{
    private readonly string _connectionString;
    private readonly int _commandTimeoutSeconds;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private LibSQLConnection? _connection;
    private bool _disposed;

    public NelknetLibsqlDatabaseClient(Microsoft.Extensions.Options.IOptions<LibsqlOptions> options)
        : this(CreateConnectionString(options.Value), options.Value.TimeoutSeconds)
    {
    }

    public NelknetLibsqlDatabaseClient(string connectionString, int commandTimeoutSeconds = 30)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        if (commandTimeoutSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(commandTimeoutSeconds), "Timeout must be greater than zero.");
        }

        _connectionString = connectionString;
        _commandTimeoutSeconds = commandTimeoutSeconds;
    }

    public async Task<LibsqlQueryResult> ExecuteAsync(string sql, object? parameters = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        await _gate.WaitAsync(ct);
        try
        {
            var connection = EnsureConnectionOpen();
            return await ExecuteStatementAsync(connection, new LibsqlStatement(sql, parameters), transaction: null, ct);
        }
        catch (Exception ex) when (ex is not LibsqlException and not OperationCanceledException)
        {
            ResetConnectionIfClosed();
            throw new LibsqlException($"libSQL execution failed: {ex.Message}", ex);
        }
        finally
        {
            _gate.Release();
        }
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
            var connection = EnsureConnectionOpen();
            using var transaction = connection.BeginTransaction();
            var results = new List<LibsqlQueryResult>(batch.Count);

            foreach (var statement in batch)
            {
                results.Add(await ExecuteStatementAsync(connection, statement, transaction, ct));
            }

            transaction.Commit();
            return results;
        }
        catch (Exception ex) when (ex is not LibsqlException and not OperationCanceledException)
        {
            ResetConnectionIfClosed();
            throw new LibsqlException($"libSQL batch execution failed: {ex.Message}", ex);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _connection?.Dispose();
        _gate.Dispose();
    }

    private static string CreateConnectionString(LibsqlOptions options)
    {
        if (options.EnableLocalReplica)
        {
            if (string.IsNullOrWhiteSpace(options.LocalReplicaPath))
            {
                throw new InvalidOperationException("Storage:Libsql:LocalReplicaPath must be configured when Storage:Libsql:EnableLocalReplica is true.");
            }

            var builder = new LibSQLConnectionStringBuilder
            {
                DataSource = ResolveLocalPath(options.LocalReplicaPath),
                SyncUrl = NormalizeDataSource(options.DataSource),
                SyncInterval = checked((int)Math.Round(options.LocalReplicaSyncIntervalSeconds * 1000d, MidpointRounding.AwayFromZero)),
                ReadYourWrites = true
            };

            if (!string.IsNullOrWhiteSpace(options.AuthToken))
            {
                builder.AuthToken = options.AuthToken;
                builder.SyncAuthToken = options.AuthToken;
            }

            return builder.ConnectionString;
        }

        var remoteBuilder = new LibSQLConnectionStringBuilder
        {
            DataSource = NormalizeDataSource(options.DataSource)
        };

        if (!string.IsNullOrWhiteSpace(options.AuthToken))
        {
            remoteBuilder.AuthToken = options.AuthToken;
        }

        return remoteBuilder.ConnectionString;
    }

    private LibSQLConnection EnsureConnectionOpen()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_connection is not null && _connection.State == ConnectionState.Open)
        {
            return _connection;
        }

        _connection?.Dispose();
        _connection = new LibSQLConnection(_connectionString);
        _connection.Open();
        return _connection;
    }

    private void ResetConnectionIfClosed()
    {
        if (_connection is null || _connection.State == ConnectionState.Open)
        {
            return;
        }

        _connection.Dispose();
        _connection = null;
    }

    private async Task<LibsqlQueryResult> ExecuteStatementAsync(
        LibSQLConnection connection,
        LibsqlStatement statement,
        DbTransaction? transaction,
        CancellationToken ct)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandTimeout = _commandTimeoutSeconds;
        command.CommandText = LibsqlCommandHelpers.BindParameters(command, statement.Sql, statement.Parameters);

        if (LibsqlCommandHelpers.IsQuery(statement.Sql))
        {
            using var reader = await command.ExecuteReaderAsync(ct);
            return await ReadQueryResultAsync(reader, ct);
        }

        var affectedRowCount = await command.ExecuteNonQueryAsync(ct);
        long? lastInsertRowId = null;

        if (LibsqlCommandHelpers.IsInsertStatement(statement.Sql))
        {
            using var rowIdCommand = connection.CreateCommand();
            rowIdCommand.Transaction = transaction;
            rowIdCommand.CommandText = "SELECT last_insert_rowid()";
            rowIdCommand.CommandTimeout = _commandTimeoutSeconds;

            var value = await rowIdCommand.ExecuteScalarAsync(ct);
            if (value is not null and not DBNull)
            {
                lastInsertRowId = Convert.ToInt64(value, CultureInfo.InvariantCulture);
            }
        }

        return new LibsqlQueryResult([], affectedRowCount, lastInsertRowId);
    }

    private static async Task<LibsqlQueryResult> ReadQueryResultAsync(DbDataReader reader, CancellationToken ct)
    {
        var columns = Enumerable.Range(0, reader.FieldCount)
            .Select(reader.GetName)
            .ToList();
        var rows = new List<LibsqlRow>();

        while (await reader.ReadAsync(ct))
        {
            var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < reader.FieldCount; index++)
            {
                values[columns[index]] = await reader.IsDBNullAsync(index, ct)
                    ? null
                    : reader.GetValue(index);
            }

            rows.Add(new LibsqlRow(columns, values));
        }

        return new LibsqlQueryResult(rows, 0, null);
    }

    private static string NormalizeDataSource(string dataSource)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataSource);

        var trimmed = dataSource.Trim();
        if (trimmed.StartsWith("libsql://", StringComparison.OrdinalIgnoreCase))
        {
            return $"https://{trimmed["libsql://".Length..]}";
        }

        return trimmed;
    }

    private static string ResolveLocalPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Path.IsPathRooted(path) ? path : Path.GetFullPath(path);
    }
}
