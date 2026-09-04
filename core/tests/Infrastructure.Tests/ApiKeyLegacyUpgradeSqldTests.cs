using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Nona.Application;
using Nona.Application.Common;
using Nona.Domain.Entities;
using Nona.Domain.Enums;
using Nona.Infrastructure;
using Nona.Infrastructure.Repositories.Libsql;
using Nona.Infrastructure.Services;
using Nona.Infrastructure.Tests.Common;
using Nona.Libsql;
using Nona.WebApi;
using Nona.WebApi.Authentication;
using Nona.WebApi.Endpoints;

namespace Nona.Infrastructure.Tests;

[NotInParallel]
public class ApiKeyLegacyUpgradeSqldTests
{
    private const string LegacySecret =
        "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";
    private const string ExpectedHash =
        "0EA37C243F60974B0D54C6B2D76CECE3F4C742492CCE48EAF81F357931D6D69E";

    [Test]
    public async Task LegacyApiKey_UpgradeAuthenticatesAndRemovesPlaintext()
    {
        var migrationsDirectory = CreatePreHashMigrationsDirectory();

        try
        {
            await using var sqld = await LocalSqldTestServer.StartAsync();
            using var database = sqld.CreateClient();

            await new LibsqlMigrationRunner(database, migrationsDirectory).RunMigrationsAsync();
            await SeedLegacyDatabaseAsync(database);

            await using var app = await StartAppAsync(sqld.Url);
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                "/api/production/feature.flag");
            request.Headers.Add(ApiKeyAuthenticationHandler.ApiKeyHeaderName, LegacySecret);

            using var response = await app.GetTestClient().SendAsync(request);
            var value = await response.Content.ReadAsStringAsync();

            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
            await Assert.That(value).IsEqualTo("legacy-value");

            var columns = await database.ExecuteAsync("PRAGMA table_info(ApiKeys)");
            await Assert.That(columns.Rows.Select(row => row.GetString("name"))).Contains("KeyHash");
            await Assert.That(columns.Rows.Select(row => row.GetString("name"))).DoesNotContain("Key");

            var stored = await database.ExecuteAsync(
                "SELECT KeyHash, Fingerprint, HashVersion FROM ApiKeys WHERE Name = 'Legacy'");
            await Assert.That(stored.Rows[0].GetString("KeyHash")).IsEqualTo(ExpectedHash);
            await Assert.That(stored.Rows[0].GetString("KeyHash")).IsNotEqualTo(LegacySecret);
            await Assert.That(stored.Rows[0].GetString("Fingerprint")).IsEqualTo("89ABCDEF");
            await Assert.That(stored.Rows[0].GetInt32("HashVersion")).IsEqualTo(1);

            await new LibsqlDatabaseInitializer(database).StartAsync(CancellationToken.None);

            var afterSecondInitialization = await database.ExecuteAsync(
                "SELECT KeyHash, Fingerprint, HashVersion FROM ApiKeys WHERE Name = 'Legacy'");
            await Assert.That(afterSecondInitialization.Rows[0].GetString("KeyHash"))
                .IsEqualTo(ExpectedHash);
            await Assert.That(afterSecondInitialization.Rows[0].GetString("Fingerprint"))
                .IsEqualTo("89ABCDEF");
            await Assert.That(afterSecondInitialization.Rows[0].GetInt32("HashVersion"))
                .IsEqualTo(1);
        }
        finally
        {
            if (Directory.Exists(migrationsDirectory))
            {
                Directory.Delete(migrationsDirectory, recursive: true);
            }
        }
    }

    [Test]
    public async Task ManualReplacement_BothAuthenticateUntilOldKeyIsDeleted()
    {
        const string oldSecret =
            "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";
        const string replacementSecret =
            "FEDCBA9876543210FEDCBA9876543210FEDCBA9876543210FEDCBA9876543210";

        await using var sqld = await LocalSqldTestServer.StartAsync();
        using var database = sqld.CreateClient();
        await using var app = await StartAppAsync(sqld.Url);
        await SeedReplacementDatabaseAsync(database);

        var repository = new LibsqlApiKeyRepository(database);
        var oldKey = CreateApiKey("Deployment", oldSecret);
        var replacementKey = CreateApiKey("Deployment replacement", replacementSecret);
        await repository.AddAsync(oldKey);
        await repository.AddAsync(replacementKey);

        using var client = app.GetTestClient();
        await AssertPublicReadAsync(client, oldSecret, HttpStatusCode.OK, "replacement-value");
        await AssertPublicReadAsync(client, replacementSecret, HttpStatusCode.OK, "replacement-value");

        await repository.DeleteAsync(oldKey.Id);

        await AssertPublicReadAsync(client, oldSecret, HttpStatusCode.Unauthorized);
        await AssertPublicReadAsync(client, replacementSecret, HttpStatusCode.OK, "replacement-value");
    }

    private static async Task SeedLegacyDatabaseAsync(ILibsqlDatabaseClient database)
    {
        await database.ExecuteBatchAsync(
            [
                new LibsqlStatement(
                    """
                    INSERT INTO Projects (Name, UrlSlug, CreatedAt, UpdatedAt)
                    VALUES ('legacy-project', 'legacy-project', '2026-09-01T00:00:00Z', '2026-09-01T00:00:00Z')
                    """),
                new LibsqlStatement(
                    """
                    INSERT INTO Environments (Name, Project, ActiveReleaseVersion, CreatedAt, UpdatedAt)
                    VALUES ('production', 'legacy-project', NULL, '2026-09-01T00:00:00Z', '2026-09-01T00:00:00Z')
                    """),
                new LibsqlStatement(
                    """
                    INSERT INTO ConfigEntries (
                        Project, Environment, Key, Value, ContentType, Scope, ActiveVersion, CreatedAt, UpdatedAt
                    )
                    VALUES (
                        'legacy-project', 'production', 'feature.flag', 'legacy-value', 'text', 1, 1,
                        '2026-09-01T00:00:00Z', '2026-09-01T00:00:00Z'
                    )
                    """),
                new LibsqlStatement(
                    """
                    INSERT INTO ApiKeys (Name, Key, Project, Environment, Scope, CreatedAt, UpdatedAt)
                    VALUES (
                        'Legacy', @Key, 'legacy-project', 'production', 1,
                        '2026-09-01T00:00:00Z', '2026-09-01T00:00:00Z'
                    )
                    """,
                    LibsqlParameters.Create(("Key", LegacySecret)))
            ]);
    }

    private static async Task SeedReplacementDatabaseAsync(ILibsqlDatabaseClient database)
    {
        await database.ExecuteBatchAsync(
            [
                new LibsqlStatement(
                    """
                    INSERT INTO Projects (Name, UrlSlug, CreatedAt, UpdatedAt)
                    VALUES (
                        'replacement-project', 'replacement-project',
                        '2026-09-01T00:00:00Z', '2026-09-01T00:00:00Z'
                    )
                    """),
                new LibsqlStatement(
                    """
                    INSERT INTO Environments (Name, Project, ActiveReleaseVersion, CreatedAt, UpdatedAt)
                    VALUES (
                        'production', 'replacement-project', NULL,
                        '2026-09-01T00:00:00Z', '2026-09-01T00:00:00Z'
                    )
                    """),
                new LibsqlStatement(
                    """
                    INSERT INTO ConfigEntries (
                        Project, Environment, Key, Value, ContentType, Scope, ActiveVersion, CreatedAt, UpdatedAt
                    )
                    VALUES (
                        'replacement-project', 'production', 'feature.flag', 'replacement-value', 'text', 1, 1,
                        '2026-09-01T00:00:00Z', '2026-09-01T00:00:00Z'
                    )
                    """)
            ]);
    }

    private static ApiKey CreateApiKey(string name, string secret)
    {
        return new ApiKey
        {
            Name = name,
            KeyHash = ApiKeySecret.Hash(secret),
            Fingerprint = ApiKeySecret.Fingerprint(secret),
            Project = "replacement-project",
            Environment = "production",
            Scope = KeyScope.Backend,
            CreatedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
        };
    }

    private static async Task AssertPublicReadAsync(
        HttpClient client,
        string secret,
        HttpStatusCode expectedStatus,
        string? expectedBody = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/production/feature.flag");
        request.Headers.Add(ApiKeyAuthenticationHandler.ApiKeyHeaderName, secret);
        using var response = await client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(expectedStatus);
        if (expectedBody is not null)
        {
            await Assert.That(await response.Content.ReadAsStringAsync()).IsEqualTo(expectedBody);
        }
    }

    private static async Task<WebApplication> StartAppAsync(string dataSource)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Storage:Type"] = "Libsql",
            ["Storage:Libsql:DataSource"] = dataSource,
            ["Storage:Libsql:ManagedPrimary:Enabled"] = "false",
            ["Jwt:Key"] = "legacy-api-key-upgrade-tests-signing-key-1234567890",
            ["Jwt:Issuer"] = "legacy-api-key-upgrade-tests",
            ["Jwt:Audience"] = "legacy-api-key-upgrade-tests"
        });
        builder.Services.AddInfrastructureServices(builder.Configuration);
        builder.Services.AddApplicationServices(builder.Configuration);
        builder.Services.AddApiServices(builder.Configuration);

        var app = builder.Build();
        app.UseExceptionHandler();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapNonaEndpoints();
        await app.StartAsync();
        return app;
    }

    private static string CreatePreHashMigrationsDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            $"nona-api-key-upgrade-sqld-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        foreach (var migration in Directory.GetFiles(ResolveMigrationsFolder(), "*.sql")
                     .Where(path => string.Compare(
                         Path.GetFileName(path),
                         "023_AddConfigEntryMetadata.sql",
                         StringComparison.OrdinalIgnoreCase) <= 0))
        {
            File.Copy(migration, Path.Combine(directory, Path.GetFileName(migration)));
        }

        return directory;
    }

    private static string ResolveMigrationsFolder()
    {
        var outputFolder = Path.Combine(AppContext.BaseDirectory, "Migrations");
        return Directory.Exists(outputFolder)
            ? outputFolder
            : Path.Combine(
                TestPaths.ResolveRepoRoot(),
                "core",
                "src",
                "Infrastructure",
                "Migrations");
    }
}
