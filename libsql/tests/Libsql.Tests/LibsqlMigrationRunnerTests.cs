using Nona.Libsql.Tests.Common;

namespace Nona.Libsql.Tests;

public class LibsqlMigrationRunnerTests
{
    [Test]
    public async Task RunMigrationsAsync_ConcurrentRunners_ApplyNonIdempotentMigrationOnce()
    {
        var migrationsFolder = Path.Combine(Path.GetTempPath(), $"nona-libsql-migrations-{Guid.NewGuid():N}");
        Directory.CreateDirectory(migrationsFolder);

        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(migrationsFolder, "001_CreateRaceItems.sql"),
                """
                CREATE TABLE RaceItems (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT
                );
                """);

            await using var server = await LocalSqldTestServer.StartAsync();
            using var firstClient = server.CreateClient();
            using var secondClient = server.CreateClient();
            using var barrier = new Barrier(2);
            var firstRunner = new LibsqlMigrationRunner(
                new CoordinatedMigrationClient(firstClient, barrier), migrationsFolder);
            var secondRunner = new LibsqlMigrationRunner(
                new CoordinatedMigrationClient(secondClient, barrier), migrationsFolder);

            await Task.WhenAll(
                firstRunner.RunMigrationsAsync(),
                secondRunner.RunMigrationsAsync());

            var table = await firstClient.ExecuteAsync(
                "SELECT COUNT(1) AS Count FROM sqlite_master WHERE type = 'table' AND name = 'RaceItems'");
            var history = await firstClient.ExecuteAsync(
                "SELECT COUNT(1) AS Count FROM __MigrationsHistory WHERE MigrationId = '001_CreateRaceItems.sql'");

            await Assert.That(table.Rows[0].GetInt32("Count")).IsEqualTo(1);
            await Assert.That(history.Rows[0].GetInt32("Count")).IsEqualTo(1);
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
        }
    }

    [Test]
    public async Task RunMigrationsAsync_AppliesEachScriptOnce()
    {
        var migrationsFolder = Path.Combine(Path.GetTempPath(), $"nona-libsql-migrations-{Guid.NewGuid():N}");
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

            await using var server = await LocalSqldTestServer.StartAsync();
            using var client = server.CreateClient();
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

        }
    }

    [Test]
    public async Task RunMigrationsAsync_AppliesRepoMigrations_ToSqld()
    {
        await using var server = await LocalSqldTestServer.StartAsync();
        using var client = server.CreateClient();
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

    private sealed class CoordinatedMigrationClient(
        ILibsqlDatabaseClient inner,
        Barrier barrier) : ILibsqlDatabaseClient
    {
        private int _coordinated;

        public async Task<LibsqlQueryResult> ExecuteAsync(
            string sql,
            object? parameters = null,
            CancellationToken ct = default)
        {
            var result = await inner.ExecuteAsync(sql, parameters, ct);
            if (sql.StartsWith("SELECT COUNT(1) FROM __MigrationsHistory", StringComparison.Ordinal)
                && Interlocked.Exchange(ref _coordinated, 1) == 0)
            {
                barrier.SignalAndWait(ct);
            }

            return result;
        }

        public Task<IReadOnlyList<LibsqlQueryResult>> ExecuteBatchAsync(
            IEnumerable<LibsqlStatement> statements,
            CancellationToken ct = default)
            => inner.ExecuteBatchAsync(statements, ct);
    }
}
