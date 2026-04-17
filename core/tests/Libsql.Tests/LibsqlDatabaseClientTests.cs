namespace Nona.Libsql.Tests;

public class LibsqlDatabaseClientTests
{
    [Test]
    public async Task ExecuteAsync_PersistsAndReadsRows_AgainstLocalFile()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"nona-libsql-local-{Guid.NewGuid():N}.db");

        try
        {
            using var client = new NelknetLibsqlDatabaseClient($"Data Source={databasePath}");

            await client.ExecuteAsync(
                """
                CREATE TABLE SampleItems (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL
                )
                """);

            var insert = await client.ExecuteAsync(
                "INSERT INTO SampleItems (Name) VALUES (@Name)",
                new { Name = "alpha" });

            var read = await client.ExecuteAsync(
                "SELECT Id, Name FROM SampleItems WHERE Id = @Id",
                new { Id = insert.LastInsertRowId });

            await Assert.That(insert.LastInsertRowId).IsNotNull();
            await Assert.That(read.Rows.Count).IsEqualTo(1);
            await Assert.That(read.Rows[0].GetString("Name")).IsEqualTo("alpha");
        }
        finally
        {
            TryDelete(databasePath);
        }
    }

    [Test]
    public async Task ExecuteBatchAsync_RunsStatementsInsideSingleClient()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"nona-libsql-batch-{Guid.NewGuid():N}.db");

        try
        {
            using var client = new NelknetLibsqlDatabaseClient($"Data Source={databasePath}");

            var results = await client.ExecuteBatchAsync(
            [
                new LibsqlStatement(
                    """
                    CREATE TABLE BatchItems (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name TEXT NOT NULL
                    )
                    """),
                new LibsqlStatement("INSERT INTO BatchItems (Name) VALUES (@Name)", new { Name = "one" }),
                new LibsqlStatement("INSERT INTO BatchItems (Name) VALUES (@Name)", new { Name = "two" }),
                new LibsqlStatement("SELECT COUNT(1) AS Count FROM BatchItems")
            ]);

            await Assert.That(results.Count).IsEqualTo(4);
            await Assert.That(results[1].LastInsertRowId).IsNotNull();
            await Assert.That(results[2].LastInsertRowId).IsNotNull();
            await Assert.That(results[3].Rows[0].GetInt32("Count")).IsEqualTo(2);
        }
        finally
        {
            TryDelete(databasePath);
        }
    }

    private static void TryDelete(string databasePath)
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
