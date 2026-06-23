namespace Nona.Libsql.Tests;

public class LibsqlMultiInstanceTests
{
    [Test]
    public async Task IndependentLibsqlClients_ShareCommittedState()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"nona-libsql-shared-{Guid.NewGuid():N}.db");

        try
        {
            using var clientA = new NelknetLibsqlDatabaseClient($"Data Source={databasePath}");
            using var clientB = new NelknetLibsqlDatabaseClient($"Data Source={databasePath}");
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
