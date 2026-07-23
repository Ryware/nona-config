namespace Nona.Libsql.Tests;

public class SqliteDatabaseClientTests
{
    [Test]
    public async Task ExecuteAsync_SupportsWalParametersReturningAndResultMapping()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            using var client = new SqliteDatabaseClient(Path.Combine(directory, "nona.db"));

            await client.ExecuteAsync(
                """
                CREATE TABLE SampleItems (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Enabled INTEGER NOT NULL,
                    OptionalValue TEXT NULL
                )
                """);

            var insert = await client.ExecuteAsync(
                """
                INSERT INTO SampleItems (Name, Enabled, OptionalValue)
                VALUES (@Name, @Enabled, @OptionalValue)
                RETURNING Id, Name, Enabled, OptionalValue
                """,
                LibsqlParameters.Create(
                    ("Name", "alpha"),
                    ("Enabled", true),
                    ("OptionalValue", (string?)null)));
            var journalMode = await client.ExecuteAsync("PRAGMA journal_mode");

            await Assert.That(insert.Rows.Count).IsEqualTo(1);
            await Assert.That(insert.AffectedRowCount).IsEqualTo(1);
            await Assert.That(insert.LastInsertRowId).IsNotNull();
            await Assert.That(insert.Rows[0].GetString("Name")).IsEqualTo("alpha");
            await Assert.That(insert.Rows[0].GetBoolean("Enabled")).IsTrue();
            await Assert.That(insert.Rows[0].GetNullableString("OptionalValue")).IsNull();
            await Assert.That(journalMode.Rows[0].GetString(0)).IsEqualTo("wal");
        }
        finally
        {
            DeleteTemporaryDirectory(directory);
        }
    }

    [Test]
    public async Task ExecuteBatchAsync_CommitsAllStatementsAndReturnsResults()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            using var client = new SqliteDatabaseClient(Path.Combine(directory, "nona.db"));
            await client.ExecuteAsync(
                "CREATE TABLE BatchItems (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL)");

            var results = await client.ExecuteBatchAsync(
            [
                new LibsqlStatement(
                    "INSERT INTO BatchItems (Name) VALUES (@Name)",
                    LibsqlParameters.Create(("Name", "one"))),
                new LibsqlStatement(
                    "INSERT INTO BatchItems (Name) VALUES (@Name) RETURNING Id, Name",
                    LibsqlParameters.Create(("Name", "two"))),
                new LibsqlStatement("SELECT COUNT(1) AS Count FROM BatchItems")
            ]);

            await Assert.That(results.Count).IsEqualTo(3);
            await Assert.That(results[0].AffectedRowCount).IsEqualTo(1);
            await Assert.That(results[0].LastInsertRowId).IsNotNull();
            await Assert.That(results[1].Rows[0].GetString("Name")).IsEqualTo("two");
            await Assert.That(results[2].Rows[0].GetInt32("Count")).IsEqualTo(2);
        }
        finally
        {
            DeleteTemporaryDirectory(directory);
        }
    }

    [Test]
    public async Task ExecuteBatchAsync_RollsBackWhenLaterStatementFails()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            using var client = new SqliteDatabaseClient(Path.Combine(directory, "nona.db"));
            await client.ExecuteAsync(
                "CREATE TABLE BatchItems (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL)");

            Exception? exception = null;
            try
            {
                await client.ExecuteBatchAsync(
                [
                    new LibsqlStatement(
                        "INSERT INTO BatchItems (Name) VALUES (@Name)",
                        LibsqlParameters.Create(("Name", "saved-first"))),
                    new LibsqlStatement(
                        "INSERT INTO BatchItems (Name) VALUES (@Name)",
                        LibsqlParameters.Create(("Name", (string?)null)))
                ]);
            }
            catch (Exception caught)
            {
                exception = caught;
            }

            var count = await client.ExecuteAsync("SELECT COUNT(1) AS Count FROM BatchItems");

            await Assert.That(exception).IsNotNull();
            await Assert.That(exception).IsTypeOf<LibsqlException>();
            await Assert.That(count.Rows[0].GetInt32("Count")).IsEqualTo(0);
        }
        finally
        {
            DeleteTemporaryDirectory(directory);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"nona-sqlite-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void DeleteTemporaryDirectory(string directory)
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
