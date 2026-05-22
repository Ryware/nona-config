using Microsoft.Extensions.Options;

namespace Nona.Libsql.Tests;

public class LibsqlRemoteIntegrationTests
{
    private const string WrapperSmokeTable = "__NonaLibsqlWrapperSmoke";

    [Test]
    public async Task RemoteLibsql_WrapperCrud_WorksWhenCredentialsProvided()
    {
        var (url, authToken) = GetRemoteCredentials();
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        using var client = CreateDirectClient(url, authToken);
        var id = Guid.NewGuid().ToString("N")[..16];

        await EnsureSmokeTableAsync(client);

        try
        {
            await client.ExecuteAsync(
                $"INSERT INTO {WrapperSmokeTable} (Id, Value, UpdatedAt) VALUES (@Id, @Value, @UpdatedAt)",
                new
                {
                    Id = id,
                    Value = "remote-value",
                    UpdatedAt = DateTime.UtcNow.ToString("O")
                });

            var stored = await client.ExecuteAsync(
                $"SELECT Value FROM {WrapperSmokeTable} WHERE Id = @Id",
                new { Id = id });

            await Assert.That(stored.Rows.Count).IsEqualTo(1);
            await Assert.That(stored.Rows[0].GetString("Value")).IsEqualTo("remote-value");

            await client.ExecuteAsync(
                $"UPDATE {WrapperSmokeTable} SET Value = @Value, UpdatedAt = @UpdatedAt WHERE Id = @Id",
                new
                {
                    Id = id,
                    Value = "remote-value-updated",
                    UpdatedAt = DateTime.UtcNow.ToString("O")
                });

            var updated = await client.ExecuteAsync(
                $"SELECT Value FROM {WrapperSmokeTable} WHERE Id = @Id",
                new { Id = id });

            await Assert.That(updated.Rows[0].GetString("Value")).IsEqualTo("remote-value-updated");

            await client.ExecuteAsync(
                $"DELETE FROM {WrapperSmokeTable} WHERE Id = @Id",
                new { Id = id });

            var deleted = await client.ExecuteAsync(
                $"SELECT COUNT(1) AS Count FROM {WrapperSmokeTable} WHERE Id = @Id",
                new { Id = id });

            await Assert.That(deleted.Rows[0].GetInt32("Count")).IsEqualTo(0);
        }
        finally
        {
            try
            {
                await client.ExecuteAsync(
                    $"DELETE FROM {WrapperSmokeTable} WHERE Id = @Id",
                    new { Id = id });
            }
            catch
            {
            }
        }
    }

    [Test]
    public async Task RemoteLibsql_EmbeddedReplica_ObservesRemoteWrites_WhenCredentialsProvided()
    {
        var (url, authToken) = GetRemoteCredentials();
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        var replicaPath = Path.Combine(Path.GetTempPath(), $"nona-libsql-embedded-{Guid.NewGuid():N}.db");
        using var directClient = CreateDirectClient(url, authToken);
        using var replicaClient = new NelknetLibsqlDatabaseClient(Options.Create(new LibsqlOptions
        {
            DataSource = url,
            AuthToken = authToken ?? string.Empty,
            EnableLocalReplica = true,
            LocalReplicaPath = replicaPath,
            LocalReplicaSyncIntervalSeconds = 0.2,
            TimeoutSeconds = 30
        }));

        var id = Guid.NewGuid().ToString("N")[..16];
        await EnsureSmokeTableAsync(directClient);

        try
        {
            await directClient.ExecuteAsync(
                $"INSERT INTO {WrapperSmokeTable} (Id, Value, UpdatedAt) VALUES (@Id, @Value, @UpdatedAt)",
                new
                {
                    Id = id,
                    Value = "embedded-value",
                    UpdatedAt = DateTime.UtcNow.ToString("O")
                });

            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < deadline)
            {
                var stored = await replicaClient.ExecuteAsync(
                    $"SELECT Value FROM {WrapperSmokeTable} WHERE Id = @Id",
                    new { Id = id });

                if (stored.Rows.Count == 1 && stored.Rows[0].GetString("Value") == "embedded-value")
                {
                    return;
                }

                await Task.Delay(200);
            }

            throw new TimeoutException("Embedded replica did not observe remote write before deadline.");
        }
        finally
        {
            try
            {
                await directClient.ExecuteAsync(
                    $"DELETE FROM {WrapperSmokeTable} WHERE Id = @Id",
                    new { Id = id });
            }
            catch
            {
            }

            try
            {
                if (File.Exists(replicaPath))
                {
                    File.Delete(replicaPath);
                }
            }
            catch
            {
            }
        }
    }

    private static async Task EnsureSmokeTableAsync(ILibsqlDatabaseClient client)
    {
        await client.ExecuteAsync(
            $"""
            CREATE TABLE IF NOT EXISTS {WrapperSmokeTable} (
                Id TEXT PRIMARY KEY,
                Value TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            )
            """);
    }

    private static NelknetLibsqlDatabaseClient CreateDirectClient(string url, string? authToken)
    {
        return new NelknetLibsqlDatabaseClient(Options.Create(new LibsqlOptions
        {
            DataSource = url,
            AuthToken = authToken ?? string.Empty,
            TimeoutSeconds = 30
        }));
    }

    private static (string? Url, string? AuthToken) GetRemoteCredentials()
    {
        return (
            Environment.GetEnvironmentVariable("NONA_LIBSQL_TEST_URL"),
            Environment.GetEnvironmentVariable("NONA_LIBSQL_TEST_AUTH_TOKEN"));
    }
}
