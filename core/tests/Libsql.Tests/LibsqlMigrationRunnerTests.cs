using Nona.Libsql.Tests.Common;

namespace Nona.Libsql.Tests;

public class LibsqlMigrationRunnerTests
{
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
                -- Create the wrapper test table.
                CREATE TABLE IF NOT EXISTS WrapperItems (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL
                );
                """);

            await File.WriteAllTextAsync(
                Path.Combine(migrationsFolder, "002_SeedWrapperItems.sql"),
                """
                /* Seed data with a semicolon inside a string to exercise script splitting. */
                INSERT INTO WrapperItems (Name) VALUES ('alpha');
                INSERT INTO WrapperItems (Name) VALUES ('beta;still-beta');
                """);

            using var handler = new FakeLibsqlMessageHandler();
            using var httpClient = CreateHttpClient(handler);
            var client = new LibsqlHttpDatabaseClient(httpClient);
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

    private static HttpClient CreateHttpClient(HttpMessageHandler handler)
    {
        return new HttpClient(handler, disposeHandler: false)
        {
            BaseAddress = new Uri("https://integration.test/"),
            Timeout = TimeSpan.FromSeconds(30)
        };
    }
}
