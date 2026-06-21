using Nona.Libsql.Tests.Common;

namespace Nona.Libsql.Tests;

public class LibsqlMigrationRunnerTests
{
    [Test]
    public async Task RunMigrationsAsync_AppliesEachScriptOnce()
    {
        var migrationsFolder = Path.Combine(Path.GetTempPath(), $"nona-libsql-migrations-{Guid.NewGuid():N}");
        var databasePath = Path.Combine(Path.GetTempPath(), $"nona-libsql-migration-db-{Guid.NewGuid():N}.db");
        Directory.CreateDirectory(migrationsFolder);

        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(migrationsFolder, "001_CreateWrapperItems.sql"),
                """
                CREATE TABLE IF NOT EXISTS WrapperItems (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL
                );
                """);

            await File.WriteAllTextAsync(
                Path.Combine(migrationsFolder, "002_SeedWrapperItems.sql"),
                """
                INSERT INTO WrapperItems (Name) VALUES ('alpha');
                INSERT INTO WrapperItems (Name) VALUES ('beta;still-beta');
                """);

            using var client = new NelknetLibsqlDatabaseClient($"Data Source={databasePath}");
            var runner = new LibsqlMigrationRunner(client, migrationsFolder);

            await runner.RunMigrationsAsync();
            await runner.RunMigrationsAsync();

            var items = await client.ExecuteAsync("SELECT Name FROM WrapperItems ORDER BY Id");
            var history = await client.ExecuteAsync("SELECT COUNT(1) AS Count FROM __MigrationsHistory");

            await Assert.That(items.Rows.Count).IsEqualTo(2);
            await Assert.That(items.Rows[0].GetString("Name")).IsEqualTo("alpha");
            await Assert.That(items.Rows[1].GetString("Name")).IsEqualTo("beta;still-beta");
            await Assert.That(history.Rows[0].GetInt32("Count")).IsEqualTo(2);
        }
        finally
        {
            try
            {
                Directory.Delete(migrationsFolder, recursive: true);
            }
            catch
            {
            }

            try
            {
                if (File.Exists(databasePath))
                {
                    File.Delete(databasePath);
                }
            }
            catch
            {
            }
        }
    }

    [Test]
    public async Task RunMigrationsAsync_AppliesRepoMigrations_ToLocalLibsqlFile()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"nona-libsql-repo-migrations-{Guid.NewGuid():N}.db");

        try
        {
            using var client = new NelknetLibsqlDatabaseClient($"Data Source={databasePath}");
            var runner = new LibsqlMigrationRunner(client, TestPaths.ResolveMigrationsFolder());

            await runner.RunMigrationsAsync();

            var projectsTable = await client.ExecuteAsync(
                """
                SELECT COUNT(1) AS Count
                FROM sqlite_master
                WHERE type = 'table' AND name = 'Projects'
                """);

            var projectColumns = await client.ExecuteAsync("PRAGMA table_info(Projects)");
            var projectColumnNames = projectColumns.Rows
                .Select(row => row.GetString("name"))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            await Assert.That(projectsTable.Rows[0].GetInt32("Count")).IsEqualTo(1);
            await Assert.That(projectColumnNames.Contains("ServerApiKey")).IsFalse();
            await Assert.That(projectColumnNames.Contains("ClientApiKey")).IsFalse();
        }
        finally
        {
            try
            {
                if (File.Exists(databasePath))
                {
                    File.Delete(databasePath);
                }
            }
            catch
            {
            }
        }
    }
}
