using Microsoft.Extensions.Options;
using Nona.Libsql.Tests.Common;

namespace Nona.Libsql.Tests;

public class LibsqlDatabaseClientTests
{
    [Test]
    public async Task Constructor_RejectsLocalReplicaOption()
    {
        Exception? exception = null;
        try
        {
            using var _ = new NelknetLibsqlDatabaseClient(Options.Create(new LibsqlOptions
            {
                DataSource = "http://primary.test",
                EnableLocalReplica = true,
                TimeoutSeconds = 30
            }));
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        await Assert.That(exception).IsNotNull();
        await Assert.That(exception).IsTypeOf<NotSupportedException>();
        await Assert.That(exception!.Message.Contains("managed sqld replica", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task Constructor_RejectsFilePathDataSource()
    {
        Exception? exception = null;
        try
        {
            using var _ = new NelknetLibsqlDatabaseClient($"Data Source={Path.Combine(Path.GetTempPath(), "nona.db")}");
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        await Assert.That(exception).IsNotNull();
        await Assert.That(exception).IsTypeOf<NotSupportedException>();
        await Assert.That(exception!.Message.Contains("sqld/libSQL HTTP data source", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task ExecuteAsync_PersistsAndReadsRows_AgainstSqld()
    {
        await using var server = await LocalSqldTestServer.StartAsync();
        using var client = server.CreateClient();

        await client.ExecuteAsync(
            """
            CREATE TABLE SampleItems (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL
            )
            """);

        var insert = await client.ExecuteAsync(
            "INSERT INTO SampleItems (Name) VALUES (@Name)",
            LibsqlParameters.Create(("Name", "alpha")));

        var read = await client.ExecuteAsync(
            "SELECT Id, Name FROM SampleItems WHERE Id = @Id",
            LibsqlParameters.Create(("Id", insert.LastInsertRowId)));

        await Assert.That(insert.LastInsertRowId).IsNotNull();
        await Assert.That(read.Rows.Count).IsEqualTo(1);
        await Assert.That(read.Rows[0].GetString("Name")).IsEqualTo("alpha");
    }

    [Test]
    public async Task ExecuteAsync_InsertsNullParameters_AgainstSqld()
    {
        await using var server = await LocalSqldTestServer.StartAsync();
        using var client = server.CreateClient();

        await client.ExecuteAsync(
            """
            CREATE TABLE NullableItems (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                OptionalValue TEXT NULL
            )
            """);

        await client.ExecuteAsync(
            "INSERT INTO NullableItems (OptionalValue) VALUES (@OptionalValue)",
            LibsqlParameters.Create(("OptionalValue", (string?)null)));

        var read = await client.ExecuteAsync("SELECT COUNT(1) AS Count FROM NullableItems WHERE OptionalValue IS NULL");

        await Assert.That(read.Rows[0].GetInt32("Count")).IsEqualTo(1);
    }

    [Test]
    public async Task ExecuteBatchAsync_RunsStatementsInsideSingleClient()
    {
        await using var server = await LocalSqldTestServer.StartAsync();
        using var client = server.CreateClient();

        var results = await client.ExecuteBatchAsync(
        [
            new LibsqlStatement(
                """
                CREATE TABLE BatchItems (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL
                )
                """),
            new LibsqlStatement("INSERT INTO BatchItems (Name) VALUES (@Name)", LibsqlParameters.Create(("Name", "one"))),
            new LibsqlStatement("INSERT INTO BatchItems (Name) VALUES (@Name)", LibsqlParameters.Create(("Name", "two"))),
            new LibsqlStatement("SELECT COUNT(1) AS Count FROM BatchItems")
        ]);

        await Assert.That(results.Count).IsEqualTo(4);
        await Assert.That(results[1].LastInsertRowId).IsNotNull();
        await Assert.That(results[2].LastInsertRowId).IsNotNull();
        await Assert.That(results[3].Rows[0].GetInt32("Count")).IsEqualTo(2);
    }

    [Test]
    public async Task ExecuteBatchAsync_RollsBackSuccessfulStatementsWhenLaterStatementFails()
    {
        await using var server = await LocalSqldTestServer.StartAsync();
        using var client = server.CreateClient();

        await client.ExecuteAsync(
            """
            CREATE TABLE BatchItems (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL
            )
            """);

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
        await Assert.That(count.Rows[0].GetInt32("Count")).IsEqualTo(0);
    }
}
