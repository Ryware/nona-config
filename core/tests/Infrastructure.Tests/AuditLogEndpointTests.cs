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
using Nona.WebApi;
using Nona.WebApi.Endpoints;

namespace Nona.Infrastructure.Tests;

public class AuditLogEndpointTests
{
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

    private static async Task<WebApplication> StartAppAsync()
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

        var app = builder.Build();
        app.UseExceptionHandler();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapNonaEndpoints();
        await app.StartAsync();
        return app;
    }
}
