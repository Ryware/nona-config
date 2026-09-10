using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nona.Application;
using Nona.Domain.Entities;
using Nona.Domain.Enums;
using Nona.Domain.Interfaces;
using Nona.Infrastructure;
using Nona.Infrastructure.Repositories.Libsql;
using Nona.Infrastructure.Tests.Common;
using Nona.Libsql;
using Nona.WebApi;
using Nona.WebApi.Endpoints;

namespace Nona.Infrastructure.Tests;

public class AuditLogEndpointTests
{
    [Test]
    [Arguments("json", 0)]
    [Arguments("json", 500)]
    [Arguments("json", 505)]
    [Arguments("csv", 0)]
    [Arguments("csv", 500)]
    [Arguments("csv", 505)]
    public async Task ExportAuditLogs_Sqld_CompletesFilteredResponseAcrossBatchBoundaries(
        string format,
        int matchingRowCount)
    {
        await using var server = await LocalSqldTestServer.StartAsync();
        using var database = server.CreateClient();
        var migrationsFolder = Path.Combine(
            TestPaths.ResolveRepoRoot(), "core", "src", "Infrastructure", "Migrations");
        await new LibsqlMigrationRunner(database, migrationsFolder).RunMigrationsAsync();
        var repository = new LibsqlAuditLogRepository(database);
        await using var app = await StartAppAsync(repository);
        using var client = app.GetTestClient();
        var token = await RegisterAdminAsync(client);
        var createdAt = new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc);

        for (var index = 0; index < matchingRowCount; index++)
        {
            await repository.AddAsync(new AuditLogEntry
            {
                Actor = "export.user@example.test",
                ActionKind = AuditActionKind.Update,
                Action = "Updated Parameter",
                Target = $"export-target-{index:D5}",
                Project = "export-project",
                Environment = "production",
                CreatedAt = createdAt
            });
        }

        foreach (var (actor, action, environment, dayOffset) in new[]
                 {
                     ("excluded.user@example.test", "Updated Parameter", "production", 0),
                     ("export.user@example.test", "Deleted Parameter", "production", 0),
                     ("export.user@example.test", "Updated Parameter", "staging", 0),
                     ("export.user@example.test", "Updated Parameter", "production", -1),
                     ("export.user@example.test", "Updated Parameter", "production", 1)
                 })
        {
            await repository.AddAsync(new AuditLogEntry
            {
                Actor = actor,
                ActionKind = AuditActionKind.Update,
                Action = action,
                Target = "excluded-target",
                Project = "export-project",
                Environment = environment,
                CreatedAt = createdAt.AddDays(dayOffset)
            });
        }

        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"/admin/audit-logs/export?format={format}" +
            "&search=export.user&action=Updated%20Parameter&environment=production" +
            "&dateFrom=2026-07-15&dateTo=2026-07-15");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();

        string[] targets;
        if (format == "json")
        {
            await Assert.That(response.Content.Headers.ContentType?.MediaType).IsEqualTo("application/json");
            using var body = JsonDocument.Parse(content);
            targets = body.RootElement.EnumerateArray()
                .Select(entry => entry.GetProperty("target").GetString()!).ToArray();
        }
        else
        {
            await Assert.That(response.Content.Headers.ContentType?.MediaType).IsEqualTo("text/csv");
            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            await Assert.That(lines[0].TrimEnd('\r'))
                .IsEqualTo("Time,Actor,ActionKind,Action,Target,Environment,SysID,Project");
            // These fixtures have no embedded commas, quotes or newlines.
            targets = lines.Skip(1).Select(line => line.Split(',')[4].Trim('"')).ToArray();
        }

        var expectedTargets = Enumerable.Range(0, matchingRowCount)
            .Select(index => $"export-target-{index:D5}").ToArray();
        await Assert.That(targets.SequenceEqual(expectedTargets)).IsTrue();
    }

    [Test]
    public async Task ListAuditLogs_AppliesFiltersAndPaginationOnTheServer()
    {
        await using var app = await StartAppAsync();
        var client = app.GetTestClient();
        var token = await RegisterAdminAsync(client);
        var repository = app.Services.GetRequiredService<IAuditLogRepository>();

        for (var index = 0; index < 17; index++)
        {
            var matches = index < 12;
            await repository.AddAsync(new AuditLogEntry
            {
                Actor = matches ? "matching.user@example.test" : "other.user@example.test",
                ActorIsSystem = false,
                ActionKind = matches ? AuditActionKind.Update : AuditActionKind.Delete,
                Action = matches ? "Updated Parameter" : "Deleted Project",
                Target = $"target-{index:D2}",
                Project = matches ? "sample-project" : "other-project",
                Environment = matches ? "production" : "staging",
                CreatedAt = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc).AddHours(index)
            });
        }

        var path = "/admin/audit-logs?page=2&pageSize=5" +
                   "&search=matching.user" +
                   "&action=Updated%20Parameter" +
                   "&environment=production" +
                   "&dateFrom=2026-07-01&dateTo=2026-07-01";
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = body.RootElement;
        await Assert.That(root.GetProperty("page").GetInt32()).IsEqualTo(2);
        await Assert.That(root.GetProperty("pageSize").GetInt32()).IsEqualTo(5);
        await Assert.That(root.GetProperty("totalCount").GetInt32()).IsEqualTo(12);
        await Assert.That(root.GetProperty("totalPages").GetInt32()).IsEqualTo(3);
        await Assert.That(root.GetProperty("items").GetArrayLength()).IsEqualTo(5);
        await Assert.That(root.GetProperty("items")[0].GetProperty("target").GetString()).IsEqualTo("target-06");
        await Assert.That(root.GetProperty("actions").EnumerateArray().Select(value => value.GetString()))
            .Contains("Updated Parameter");
        await Assert.That(root.GetProperty("environments").EnumerateArray().Select(value => value.GetString()))
            .Contains("production");
    }

    [Test]
    public async Task ListAuditLogs_FiltersGlobalScopeEntries()
    {
        await using var app = await StartAppAsync();
        var client = app.GetTestClient();
        var token = await RegisterAdminAsync(client);
        var repository = app.Services.GetRequiredService<IAuditLogRepository>();
        await repository.AddAsync(new AuditLogEntry
        {
            Actor = "global.user@example.test",
            ActionKind = AuditActionKind.Create,
            Action = "Created Project",
            Target = "global-target",
            Environment = null,
            CreatedAt = new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc)
        });
        await repository.AddAsync(new AuditLogEntry
        {
            Actor = "production.user@example.test",
            ActionKind = AuditActionKind.Create,
            Action = "Created Project",
            Target = "production-target",
            Environment = "production",
            CreatedAt = new DateTime(2026, 7, 15, 13, 0, 0, DateTimeKind.Utc)
        });

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/admin/audit-logs?page=1&pageSize=25&environment={AuditLogFilter.GlobalScopeEnvironment}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

        await Assert.That(body.RootElement.GetProperty("totalCount").GetInt32()).IsEqualTo(1);
        await Assert.That(body.RootElement.GetProperty("items")[0].GetProperty("target").GetString())
            .IsEqualTo("global-target");
        await Assert.That(body.RootElement.GetProperty("environments").EnumerateArray().Select(value => value.GetString()))
            .Contains(AuditLogFilter.GlobalScopeEnvironment);
    }

    [Test]
    public async Task ExportAuditLogs_StreamsAtLeastTenThousandFilteredRows()
    {
        await using var app = await StartAppAsync();
        var client = app.GetTestClient();
        var token = await RegisterAdminAsync(client);
        var repository = app.Services.GetRequiredService<IAuditLogRepository>();
        var createdAt = new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc);

        for (var index = 0; index < 10_000; index++)
        {
            await repository.AddAsync(new AuditLogEntry
            {
                Actor = "export.user@example.test",
                ActorIsSystem = false,
                ActionKind = AuditActionKind.Update,
                Action = "Updated Parameter",
                Target = $"export-target-{index:D5}",
                Project = "export-project",
                Environment = "production",
                CreatedAt = createdAt.AddTicks(index)
            });
        }

        await repository.AddAsync(new AuditLogEntry
        {
            Actor = "excluded.user@example.test",
            ActorIsSystem = false,
            ActionKind = AuditActionKind.Delete,
            Action = "Deleted Project",
            Target = "excluded-target",
            Project = "excluded-project",
            Environment = "staging",
            CreatedAt = createdAt
        });

        var path = "/admin/audit-logs/export?format=csv" +
                   "&search=export.user" +
                   "&action=Updated%20Parameter" +
                   "&environment=production" +
                   "&dateFrom=2026-07-15&dateTo=2026-07-15";
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();

        await Assert.That(response.Content.Headers.ContentType?.MediaType).IsEqualTo("text/csv");
        await Assert.That(response.Content.Headers.ContentDisposition?.FileName).Contains("audit-logs-");
        await Assert.That(content.Split('\n', StringSplitOptions.RemoveEmptyEntries)).Count().IsEqualTo(10_001);
        await Assert.That(content).Contains("export-target-09999");
        await Assert.That(content).DoesNotContain("excluded-target");
    }

    [Test]
    public async Task ExportAuditLogs_ReturnsJsonAndRejectsUnknownFormats()
    {
        await using var app = await StartAppAsync();
        var client = app.GetTestClient();
        var token = await RegisterAdminAsync(client);
        var repository = app.Services.GetRequiredService<IAuditLogRepository>();
        await repository.AddAsync(new AuditLogEntry
        {
            Actor = "json.user@example.test",
            ActorIsSystem = false,
            ActionKind = AuditActionKind.Create,
            Action = "Created Project",
            Target = "json-export-target",
            Project = "sample-project",
            Environment = null,
            CreatedAt = new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc)
        });

        using var jsonRequest = new HttpRequestMessage(
            HttpMethod.Get,
            "/admin/audit-logs/export?format=json&search=json-export-target");
        jsonRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var jsonResponse = await client.SendAsync(jsonRequest);
        jsonResponse.EnsureSuccessStatusCode();
        using var json = await JsonDocument.ParseAsync(await jsonResponse.Content.ReadAsStreamAsync());

        await Assert.That(jsonResponse.Content.Headers.ContentType?.MediaType).IsEqualTo("application/json");
        await Assert.That(json.RootElement.GetArrayLength()).IsEqualTo(1);
        await Assert.That(json.RootElement[0].GetProperty("target").GetString()).IsEqualTo("json-export-target");
        await Assert.That(json.RootElement[0].GetProperty("environment").GetString()).IsEqualTo("Global Scope");

        using var invalidRequest = new HttpRequestMessage(
            HttpMethod.Get,
            "/admin/audit-logs/export?format=xml");
        invalidRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var invalidResponse = await client.SendAsync(invalidRequest);
        await Assert.That(invalidResponse.StatusCode).IsEqualTo(System.Net.HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task ExportAuditLogs_NeutralizesFormulaLeadingCsvCells()
    {
        await using var app = await StartAppAsync();
        var client = app.GetTestClient();
        var token = await RegisterAdminAsync(client);
        var repository = app.Services.GetRequiredService<IAuditLogRepository>();
        await repository.AddAsync(new AuditLogEntry
        {
            Actor = "=HYPERLINK(\"https://example.test\")",
            ActorIsSystem = false,
            ActionKind = AuditActionKind.Update,
            Action = "Updated Parameter",
            Target = "+SUM(1,1)",
            Project = "@malicious-project",
            Environment = "-2+3",
            CreatedAt = new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc)
        });

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/admin/audit-logs/export?format=csv");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();

        await Assert.That(content).Contains("\"'=HYPERLINK(\"\"https://example.test\"\")\"");
        await Assert.That(content).Contains("\"'+SUM(1,1)\"");
        await Assert.That(content).Contains("\"'@malicious-project\"");
        await Assert.That(content).Contains("\"'-2+3\"");
    }

    private static async Task<string> RegisterAdminAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(
            "/auth/register",
            new { email = $"admin-{Guid.NewGuid():N}@example.test", password = "Password123!" });
        response.EnsureSuccessStatusCode();

        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return body.RootElement.GetProperty("token").GetString()
            ?? throw new InvalidOperationException("Register response did not include a token.");
    }

    private static async Task<WebApplication> StartAppAsync(IAuditLogRepository? auditLogRepository = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Storage:Type"] = "InMemory",
            ["Jwt:Key"] = "audit-log-endpoint-tests-signing-key-1234567890",
            ["Jwt:Issuer"] = "audit-log-endpoint-tests",
            ["Jwt:Audience"] = "audit-log-endpoint-tests"
        });

        builder.Services.AddOpenApi();
        builder.Services.AddInfrastructureServices(builder.Configuration);
        builder.Services.AddApplicationServices(builder.Configuration);
        builder.Services.AddApiServices(builder.Configuration);

        if (auditLogRepository is not null)
        {
            builder.Services.AddSingleton(auditLogRepository);
        }

        var app = builder.Build();
        app.UseExceptionHandler();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapNonaEndpoints();
        await app.StartAsync();
        return app;
    }
}
