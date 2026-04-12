using Microsoft.Data.Sqlite;
using System.Data;

namespace Nona.Libsql;

internal sealed class LibsqlSqliteDatabase : IDisposable
{
    private readonly string _connectionString;
    private SqliteConnection? _connection;

    public LibsqlSqliteDatabase(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<SqliteConnection> GetConnectionAsync(CancellationToken ct = default)
    {
        if (_connection is not null && _connection.State == ConnectionState.Open)
        {
            return _connection;
        }

        _connection = new SqliteConnection(_connectionString);
        await _connection.OpenAsync(ct);
        return _connection;
    }

    public async Task InitializeDatabaseAsync(string migrationsFolder, CancellationToken ct = default)
    {
        var migrationRunner = new LibsqlMigrationRunner(new LocalSqliteDatabaseClient(this), migrationsFolder);
        await migrationRunner.RunMigrationsAsync(ct);
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }
}

internal sealed class LocalSqliteDatabaseClient : ILibsqlDatabaseClient
{
    private readonly LibsqlSqliteDatabase _database;

    public LocalSqliteDatabaseClient(LibsqlSqliteDatabase database)
    {
        _database = database;
    }

    public async Task<LibsqlQueryResult> ExecuteAsync(string sql, object? parameters = null, CancellationToken ct = default)
    {
        var connection = await _database.GetConnectionAsync(ct);
        return await LibsqlSqliteCommandExecutor.ExecuteAsync(connection, sql, parameters, ct: ct);
    }

    public async Task<IReadOnlyList<LibsqlQueryResult>> ExecuteBatchAsync(
        IEnumerable<LibsqlStatement> statements,
        CancellationToken ct = default)
    {
        var connection = await _database.GetConnectionAsync(ct);
        return await LibsqlSqliteCommandExecutor.ExecuteBatchAsync(connection, statements, ct);
    }
}
