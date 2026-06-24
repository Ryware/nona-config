using Nona.Libsql.Tests.Common;

namespace Nona.Libsql.Tests;

public class LibsqlMultiInstanceTests
{
    [Test]
    public async Task IndependentLibsqlClients_ShareCommittedState()
    {
        await using var server = await LocalSqldTestServer.StartAsync();
        using var clientA = server.CreateClient();
        using var clientB = server.CreateClient();
        var id = Guid.NewGuid().ToString("N")[..12];

        await clientA.ExecuteAsync(
            """
            CREATE TABLE IF NOT EXISTS SharedItems (
                Id TEXT PRIMARY KEY,
                Value TEXT NOT NULL
            )
            """);

        await clientA.ExecuteAsync(
            "INSERT INTO SharedItems (Id, Value) VALUES (@Id, @Value)",
            LibsqlParameters.Create(
                ("Id", id),
                ("Value", "value-written-from-instance-a")));

        var readFromInstanceB = await clientB.ExecuteAsync(
            "SELECT Value FROM SharedItems WHERE Id = @Id",
            LibsqlParameters.Create(("Id", id)));

        await Assert.That(readFromInstanceB.Rows.Count).IsEqualTo(1);
        await Assert.That(readFromInstanceB.Rows[0].GetString("Value")).IsEqualTo("value-written-from-instance-a");

        await clientB.ExecuteAsync(
            "DELETE FROM SharedItems WHERE Id = @Id",
            LibsqlParameters.Create(("Id", id)));

        var readAgainFromInstanceA = await clientA.ExecuteAsync(
            "SELECT COUNT(1) AS Count FROM SharedItems WHERE Id = @Id",
            LibsqlParameters.Create(("Id", id)));

        await Assert.That(readAgainFromInstanceA.Rows[0].GetInt32("Count")).IsEqualTo(0);
    }
}
