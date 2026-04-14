using Microsoft.Data.Sqlite;
using System.Data;

namespace Nona.Infrastructure.Repositories.Sqlite;

public class SqliteDbContext : IDisposable
{
    private readonly string _connectionString;
    private SqliteConnection? _connection;

    public SqliteDbContext(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<SqliteConnection> GetConnectionAsync(CancellationToken ct = default)
    {
        if (_connection != null && _connection.State == ConnectionState.Open)
            return _connection;

        _connection = new SqliteConnection(_connectionString);
        await _connection.OpenAsync(ct);

        return _connection;
    }

    public async Task InitializeDatabaseAsync(string migrationsFolder, CancellationToken ct = default)
    {
        var migrationRunner = new SqliteMigrationRunner(this, migrationsFolder);
        await migrationRunner.RunMigrationsAsync(ct);
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }
}
