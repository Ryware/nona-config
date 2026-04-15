using System.Net.Http.Headers;

namespace Nona.Libsql.Tests;

public class LibsqlRemoteIntegrationTests
{
    private const string WrapperSmokeTable = "__NonaLibsqlWrapperSmoke";

    [Test]
    public async Task RemoteLibsql_WrapperCrud_WorksWhenCredentialsProvided()
    {
        var url = Environment.GetEnvironmentVariable("NONA_LIBSQL_TEST_URL");
        var authToken = Environment.GetEnvironmentVariable("NONA_LIBSQL_TEST_AUTH_TOKEN");

        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(authToken))
        {
            return;
        }

        using var httpClient = CreateHttpClient(url, authToken);
        var client = new LibsqlHttpDatabaseClient(httpClient);
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
    public async Task RemoteLibsql_TwoClients_ObserveSharedState_WhenCredentialsProvided()
    {
        var url = Environment.GetEnvironmentVariable("NONA_LIBSQL_TEST_URL");
        var authToken = Environment.GetEnvironmentVariable("NONA_LIBSQL_TEST_AUTH_TOKEN");

        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(authToken))
        {
            return;
        }

        using var httpClientA = CreateHttpClient(url, authToken);
        using var httpClientB = CreateHttpClient(url, authToken);

        var clientA = new LibsqlHttpDatabaseClient(httpClientA);
        var clientB = new LibsqlHttpDatabaseClient(httpClientB);
        var id = Guid.NewGuid().ToString("N")[..16];

        await EnsureSmokeTableAsync(clientA);

        try
        {
            await clientA.ExecuteAsync(
                $"INSERT INTO {WrapperSmokeTable} (Id, Value, UpdatedAt) VALUES (@Id, @Value, @UpdatedAt)",
                new
                {
                    Id = id,
                    Value = "shared-remote-value",
                    UpdatedAt = DateTime.UtcNow.ToString("O")
                });

            var readFromClientB = await clientB.ExecuteAsync(
                $"SELECT Value FROM {WrapperSmokeTable} WHERE Id = @Id",
                new { Id = id });

            await Assert.That(readFromClientB.Rows.Count).IsEqualTo(1);
            await Assert.That(readFromClientB.Rows[0].GetString("Value")).IsEqualTo("shared-remote-value");

            await clientB.ExecuteAsync(
                $"DELETE FROM {WrapperSmokeTable} WHERE Id = @Id",
                new { Id = id });

            var readAgainFromClientA = await clientA.ExecuteAsync(
                $"SELECT COUNT(1) AS Count FROM {WrapperSmokeTable} WHERE Id = @Id",
                new { Id = id });

            await Assert.That(readAgainFromClientA.Rows[0].GetInt32("Count")).IsEqualTo(0);
        }
        finally
        {
            try
            {
                await clientA.ExecuteAsync(
                    $"DELETE FROM {WrapperSmokeTable} WHERE Id = @Id",
                    new { Id = id });
            }
            catch
            {
            }
        }
    }

    private static async Task EnsureSmokeTableAsync(LibsqlHttpDatabaseClient client)
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

    private static HttpClient CreateHttpClient(string url, string authToken)
    {
        var httpClient = new HttpClient
        {
            BaseAddress = new Uri($"{LibsqlHttpDatabaseClient.NormalizeBaseUrl(url)}/"),
            Timeout = TimeSpan.FromSeconds(30)
        };
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
        return httpClient;
    }
}
