using Microsoft.Extensions.Options;

namespace Nona.Libsql.Tests;

public class LibsqlRemoteIntegrationTests
{
    private const string WrapperSmokeTable = "__NonaLibsqlWrapperSmoke";
    private const string NullSmokeTable = "__NonaLibsqlNullSmoke";

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
                LibsqlParameters.Create(
                    ("Id", id),
                    ("Value", "remote-value"),
                    ("UpdatedAt", DateTime.UtcNow.ToString("O"))));

            var stored = await client.ExecuteAsync(
                $"SELECT Value FROM {WrapperSmokeTable} WHERE Id = @Id",
                LibsqlParameters.Create(("Id", id)));

            await Assert.That(stored.Rows.Count).IsEqualTo(1);
            await Assert.That(stored.Rows[0].GetString("Value")).IsEqualTo("remote-value");

            await client.ExecuteAsync(
                $"UPDATE {WrapperSmokeTable} SET Value = @Value, UpdatedAt = @UpdatedAt WHERE Id = @Id",
                LibsqlParameters.Create(
                    ("Id", id),
                    ("Value", "remote-value-updated"),
                    ("UpdatedAt", DateTime.UtcNow.ToString("O"))));

            var updated = await client.ExecuteAsync(
                $"SELECT Value FROM {WrapperSmokeTable} WHERE Id = @Id",
                LibsqlParameters.Create(("Id", id)));

            await Assert.That(updated.Rows[0].GetString("Value")).IsEqualTo("remote-value-updated");

            await client.ExecuteAsync(
                $"DELETE FROM {WrapperSmokeTable} WHERE Id = @Id",
                LibsqlParameters.Create(("Id", id)));

            var deleted = await client.ExecuteAsync(
                $"SELECT COUNT(1) AS Count FROM {WrapperSmokeTable} WHERE Id = @Id",
                LibsqlParameters.Create(("Id", id)));

            await Assert.That(deleted.Rows[0].GetInt32("Count")).IsEqualTo(0);
        }
        finally
        {
            try
            {
                await client.ExecuteAsync(
                    $"DELETE FROM {WrapperSmokeTable} WHERE Id = @Id",
                    LibsqlParameters.Create(("Id", id)));
            }
            catch
            {
            }
        }
    }

    [Test]
    public async Task RemoteLibsql_LocalReplicaOption_IsRejected()
    {
        Exception? exception = null;
        try
        {
            using var _ = new NelknetLibsqlDatabaseClient(Options.Create(new LibsqlOptions
            {
                DataSource = "http://primary.test",
                EnableLocalReplica = true,
                LocalReplicaPath = Path.Combine(Path.GetTempPath(), $"nona-libsql-embedded-{Guid.NewGuid():N}.db"),
                LocalReplicaSyncIntervalSeconds = 0.2,
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
    public async Task RemoteLibsql_NullParameters_AreInserted_WhenCredentialsProvided()
    {
        var (url, authToken) = GetRemoteCredentials();
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        using var client = CreateDirectClient(url, authToken);
        var id = Guid.NewGuid().ToString("N")[..16];

        await client.ExecuteAsync(
            $"""
            CREATE TABLE IF NOT EXISTS {NullSmokeTable} (
                Id TEXT PRIMARY KEY,
                OptionalValue TEXT NULL
            )
            """);

        try
        {
            await client.ExecuteAsync(
                $"INSERT INTO {NullSmokeTable} (Id, OptionalValue) VALUES (@Id, @OptionalValue)",
                LibsqlParameters.Create(
                    ("Id", id),
                    ("OptionalValue", (string?)null)));

            var stored = await client.ExecuteAsync(
                $"SELECT COUNT(1) AS Count FROM {NullSmokeTable} WHERE Id = @Id AND OptionalValue IS NULL",
                LibsqlParameters.Create(("Id", id)));

            await Assert.That(stored.Rows[0].GetInt32("Count")).IsEqualTo(1);
        }
        finally
        {
            try
            {
                await client.ExecuteAsync(
                    $"DELETE FROM {NullSmokeTable} WHERE Id = @Id",
                    LibsqlParameters.Create(("Id", id)));
            }
            catch
            {
            }
        }
    }

    [Test]
    public async Task RemoteLibsql_UserRepositoryInsertShape_InsertsRow_WhenCredentialsProvided()
    {
        var (url, authToken) = GetRemoteCredentials();
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        using var client = CreateDirectClient(url, authToken);
        var email = $"remote-user-{Guid.NewGuid():N}@example.com";

        await client.ExecuteAsync(
            """
            CREATE TABLE IF NOT EXISTS __NonaLibsqlUserInsertSmoke (
                Email TEXT NOT NULL PRIMARY KEY COLLATE NOCASE,
                Name TEXT NOT NULL DEFAULT '',
                PasswordHash TEXT,
                PasswordSalt TEXT,
                Role INTEGER NOT NULL DEFAULT 0,
                Scope INTEGER NOT NULL DEFAULT 3,
                IsAdmin INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                PasswordResetToken TEXT,
                InviteTokenHash TEXT NULL
            )
            """);

        try
        {
            var insert = await client.ExecuteAsync(
                """
                INSERT INTO __NonaLibsqlUserInsertSmoke (Email, Name, PasswordHash, PasswordSalt, Role, Scope, IsAdmin, CreatedAt, UpdatedAt)
                VALUES (@Email, @Name, @PasswordHash, @PasswordSalt, @Role, @Scope, @IsAdmin, @CreatedAt, @UpdatedAt)
                """,
                LibsqlParameters.Create(
                    ("Email", email),
                    ("Name", email),
                    ("PasswordHash", "$2a$12$aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"),
                    ("PasswordSalt", string.Empty),
                    ("Role", 0),
                    ("Scope", 3),
                    ("IsAdmin", true),
                    ("CreatedAt", "2026-06-03T12:00:00.0000000Z"),
                    ("UpdatedAt", "2026-06-03T12:00:00.0000000Z")));

            var stored = await client.ExecuteAsync(
                "SELECT COUNT(1) AS Count FROM __NonaLibsqlUserInsertSmoke WHERE Email = @Email",
                LibsqlParameters.Create(("Email", email)));

            await Assert.That(insert.AffectedRowCount).IsEqualTo(1);
            await Assert.That(stored.Rows[0].GetInt32("Count")).IsEqualTo(1);
        }
        finally
        {
            try
            {
                await client.ExecuteAsync(
                    "DELETE FROM __NonaLibsqlUserInsertSmoke WHERE Email = @Email",
                    LibsqlParameters.Create(("Email", email)));
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
