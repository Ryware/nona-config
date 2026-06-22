using Nelknet.LibSQL.Data;
using System.Data;
using System.Data.Common;
using System.Globalization;

namespace Nona.Libsql;

public sealed class NelknetLibsqlDatabaseClient : ILibsqlDatabaseClient, IDisposable
{
    private readonly string _readConnectionString;
    private readonly string _writeConnectionString;
    private readonly int _commandTimeoutSeconds;
    private readonly bool _shareReadWriteConnection;
    private readonly bool _syncReadConnectionAfterWrites;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private LibSQLConnection? _readConnection;
    private LibSQLConnection? _writeConnection;
    private bool _disposed;

    public NelknetLibsqlDatabaseClient(Microsoft.Extensions.Options.IOptions<LibsqlOptions> options)
        : this(CreateConnectionStrings(options.Value), options.Value.TimeoutSeconds)
    {
    }

    public NelknetLibsqlDatabaseClient(string connectionString, int commandTimeoutSeconds = 30)
        : this(new LibsqlConnectionStrings(connectionString, connectionString, SyncReadConnectionAfterWrites: false), commandTimeoutSeconds)
    {
    }

    private NelknetLibsqlDatabaseClient(LibsqlConnectionStrings connectionStrings, int commandTimeoutSeconds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionStrings.ReadConnectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionStrings.WriteConnectionString);

        if (commandTimeoutSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(commandTimeoutSeconds), "Timeout must be greater than zero.");
        }

        _readConnectionString = connectionStrings.ReadConnectionString;
        _writeConnectionString = connectionStrings.WriteConnectionString;
        _commandTimeoutSeconds = commandTimeoutSeconds;
        _syncReadConnectionAfterWrites = connectionStrings.SyncReadConnectionAfterWrites;
        _shareReadWriteConnection = string.Equals(
            _readConnectionString,
            _writeConnectionString,
            StringComparison.Ordinal);
    }

    public async Task<LibsqlQueryResult> ExecuteAsync(string sql, object? parameters = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        await _gate.WaitAsync(ct);
        try
        {
            var target = GetConnectionTarget(sql);
            var connection = EnsureConnectionOpen(target);
            var result = await ExecuteStatementAsync(connection, new LibsqlStatement(sql, parameters), transaction: null, ct);
            if (target == LibsqlConnectionTarget.Write)
            {
                await SyncReadConnectionAfterWriteAsync(ct);
            }

            return result;
        }
        catch (Exception ex) when (ex is not LibsqlException and not OperationCanceledException)
        {
            ResetClosedConnections();
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
            var target = batch.Any(statement => GetConnectionTarget(statement.Sql) == LibsqlConnectionTarget.Write)
                ? LibsqlConnectionTarget.Write
                : LibsqlConnectionTarget.Read;
            var connection = EnsureConnectionOpen(target);
            using var transaction = connection.BeginTransaction();
            var results = new List<LibsqlQueryResult>(batch.Count);

            foreach (var statement in batch)
            {
                results.Add(await ExecuteStatementAsync(connection, statement, transaction, ct));
            }

            transaction.Commit();
            if (target == LibsqlConnectionTarget.Write)
            {
                await SyncReadConnectionAfterWriteAsync(ct);
            }

            return results;
        }
        catch (Exception ex) when (ex is not LibsqlException and not OperationCanceledException)
        {
            ResetClosedConnections();
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
        _readConnection?.Dispose();
        if (!ReferenceEquals(_writeConnection, _readConnection))
        {
            _writeConnection?.Dispose();
        }

        _gate.Dispose();
    }

    private static LibsqlConnectionStrings CreateConnectionStrings(LibsqlOptions options)
    {
        var writeConnectionString = CreateDirectConnectionString(options.DataSource, options.AuthToken);

        if (options.EnableLocalReplica)
        {
            if (string.IsNullOrWhiteSpace(options.LocalReplicaPath))
            {
                throw new InvalidOperationException("Storage:Libsql:LocalReplicaPath must be configured when Storage:Libsql:EnableLocalReplica is true.");
            }

            var builder = new LibSQLConnectionStringBuilder
            {
                DataSource = ResolveLocalReplicaPath(options.LocalReplicaPath),
                SyncUrl = NormalizeDataSource(options.DataSource),
                SyncInterval = checked((int)Math.Round(options.LocalReplicaSyncIntervalSeconds * 1000d, MidpointRounding.AwayFromZero)),
                ReadYourWrites = true
            };

            if (!string.IsNullOrWhiteSpace(options.AuthToken))
            {
                builder.AuthToken = options.AuthToken;
                builder.SyncAuthToken = options.AuthToken;
            }

            return new LibsqlConnectionStrings(
                builder.ConnectionString,
                writeConnectionString,
                SyncReadConnectionAfterWrites: true);
        }

        return new LibsqlConnectionStrings(
            writeConnectionString,
            writeConnectionString,
            SyncReadConnectionAfterWrites: false);
    }

    private static string CreateDirectConnectionString(string dataSource, string authToken)
    {
        var builder = new LibSQLConnectionStringBuilder
        {
            DataSource = NormalizeDataSource(dataSource)
        };

        if (!string.IsNullOrWhiteSpace(authToken))
        {
            builder.AuthToken = authToken;
        }

        return builder.ConnectionString;
    }

    private LibSQLConnection EnsureConnectionOpen(LibsqlConnectionTarget target)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_shareReadWriteConnection)
        {
            var sharedConnection = _readConnection ?? _writeConnection;
            if (sharedConnection is not null && sharedConnection.State == ConnectionState.Open)
            {
                _readConnection = sharedConnection;
                _writeConnection = sharedConnection;
                return sharedConnection;
            }

            sharedConnection?.Dispose();
            sharedConnection = new LibSQLConnection(_readConnectionString);
            sharedConnection.Open();

            _readConnection = sharedConnection;
            _writeConnection = sharedConnection;
            return sharedConnection;
        }

        var connection = target == LibsqlConnectionTarget.Read
            ? _readConnection
            : _writeConnection;

        if (connection is not null && connection.State == ConnectionState.Open)
        {
            return connection;
        }

        connection?.Dispose();
        connection = new LibSQLConnection(target == LibsqlConnectionTarget.Read
            ? _readConnectionString
            : _writeConnectionString);
        connection.Open();

        if (target == LibsqlConnectionTarget.Read)
        {
            _readConnection = connection;
        }
        else
        {
            _writeConnection = connection;
        }

        return connection;
    }

    private void ResetClosedConnections()
    {
        ResetClosedConnection(ref _readConnection);
        ResetClosedConnection(ref _writeConnection);
    }

    private static void ResetClosedConnection(ref LibSQLConnection? connection)
    {
        if (connection is null || connection.State == ConnectionState.Open)
        {
            return;
        }

        connection.Dispose();
        connection = null;
    }

    private static LibsqlConnectionTarget GetConnectionTarget(string sql)
        => LibsqlCommandHelpers.IsQuery(sql) ? LibsqlConnectionTarget.Read : LibsqlConnectionTarget.Write;

    private async Task SyncReadConnectionAfterWriteAsync(CancellationToken ct)
    {
        if (!_syncReadConnectionAfterWrites || _shareReadWriteConnection)
        {
            return;
        }

        var readConnection = EnsureConnectionOpen(LibsqlConnectionTarget.Read);
        await readConnection.SyncAsync(ct);
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

        if (LibsqlCommandHelpers.ReturnsRows(statement.Sql))
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

    private static string ResolveLocalReplicaPath(string path)
    {
        var resolvedPath = ResolveLocalPath(path);
        var directory = Path.GetDirectoryName(resolvedPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        return resolvedPath;
    }

    private readonly record struct LibsqlConnectionStrings(
        string ReadConnectionString,
        string WriteConnectionString,
        bool SyncReadConnectionAfterWrites);

    private enum LibsqlConnectionTarget
    {
        Read,
        Write
    }
}
