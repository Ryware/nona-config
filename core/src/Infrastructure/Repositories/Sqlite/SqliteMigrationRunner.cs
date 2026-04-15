using Microsoft.Data.Sqlite;

namespace Nona.Infrastructure.Repositories.Sqlite;

public class SqliteMigrationRunner
{
    private readonly SqliteDbContext _dbContext;
    private readonly string _migrationsFolder;

    public SqliteMigrationRunner(SqliteDbContext dbContext, string migrationsFolder)
    {
        _dbContext = dbContext;
        _migrationsFolder = migrationsFolder;
    }

    public async Task RunMigrationsAsync(CancellationToken ct = default)
    {
        var connection = await _dbContext.GetConnectionAsync(ct);

        // Create migrations tracking table if it doesn't exist
        await CreateMigrationsTableAsync(connection, ct);

        // Get all SQL files sorted by name
        var migrationFiles = Directory.GetFiles(_migrationsFolder, "*.sql")
            .OrderBy(f => Path.GetFileName(f))
            .ToList();

        foreach (var migrationFile in migrationFiles)
        {
            var migrationName = Path.GetFileName(migrationFile);

            // Check if migration has already been applied
            if (await IsMigrationAppliedAsync(connection, migrationName, ct))
            {
                Console.WriteLine($"??  Skipping {migrationName} (already applied)");
                continue;
            }

            Console.WriteLine($"?? Applying {migrationName}...");

            // Read and execute the SQL script
            var sql = await File.ReadAllTextAsync(migrationFile, ct);

            using var transaction = connection.BeginTransaction();
            try
            {
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = sql;
                await command.ExecuteNonQueryAsync(ct);

                // Record the migration
                await RecordMigrationAsync(connection, migrationName, transaction, ct);

                await transaction.CommitAsync(ct);
                Console.WriteLine($"? Applied {migrationName}");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
                Console.WriteLine($"? Failed to apply {migrationName}: {ex.Message}");
                throw;
            }
        }

        Console.WriteLine("? All migrations applied successfully!");
    }

    private async Task CreateMigrationsTableAsync(SqliteConnection connection, CancellationToken ct)
    {
        var sql = @"
            CREATE TABLE IF NOT EXISTS __MigrationsHistory (
                MigrationId TEXT PRIMARY KEY,
                AppliedAt TEXT NOT NULL
            );";

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(ct);
    }

    private async Task<bool> IsMigrationAppliedAsync(SqliteConnection connection, string migrationName, CancellationToken ct)
    {
        var sql = "SELECT COUNT(1) FROM __MigrationsHistory WHERE MigrationId = @MigrationId";

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@MigrationId", migrationName);

        var count = Convert.ToInt32(await command.ExecuteScalarAsync(ct));
        return count > 0;
    }

    private async Task RecordMigrationAsync(SqliteConnection connection, string migrationName, SqliteTransaction transaction, CancellationToken ct)
    {
        var sql = "INSERT INTO __MigrationsHistory (MigrationId, AppliedAt) VALUES (@MigrationId, @AppliedAt)";

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.Parameters.AddWithValue("@MigrationId", migrationName);
        command.Parameters.AddWithValue("@AppliedAt", DateTime.UtcNow.ToString("O"));

        await command.ExecuteNonQueryAsync(ct);
    }
}
