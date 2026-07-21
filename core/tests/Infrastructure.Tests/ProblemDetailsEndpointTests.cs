using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nona.Application;
using Nona.Infrastructure;
using Nona.WebApi;
using Nona.WebApi.Endpoints;

namespace Nona.Infrastructure.Tests;

public class ProblemDetailsEndpointTests
{
    [Test]
    [Arguments("/api/production", 401, "Unauthorized")]
    [Arguments("/admin/projects", 401, "Unauthorized")]
    public async Task AuthenticationFailures_ReturnProblemDetails(
        string path,
        int expectedStatus,
        string expectedTitle)
    {
        await using var app = await StartAppAsync();

        using var response = await app.GetTestClient().GetAsync(path);
        using var body = await ParseJsonAsync(response);

        await AssertProblemAsync(response, body.RootElement, expectedStatus, expectedTitle);
    }

    [Test]
    public async Task UnhandledException_ReturnsSanitizedProblemDetails()
    {
        await using var app = await StartAppAsync();

        using var response = await app.GetTestClient().GetAsync("/throw");
        using var body = await ParseJsonAsync(response);

        await AssertProblemAsync(response, body.RootElement, 500, "Internal Server Error");
        await Assert.That(body.RootElement.GetProperty("detail").GetString())
            .IsEqualTo("An unexpected error occurred.");
        await Assert.That(body.RootElement.GetRawText()).DoesNotContain("test exception");
    }

    private static async Task<WebApplication> StartAppAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Storage:Type"] = "InMemory",
            ["Jwt:Key"] = "problem-details-tests-signing-key-1234567890",
            ["Jwt:Issuer"] = "problem-details-tests",
            ["Jwt:Audience"] = "problem-details-tests"
        });

        builder.Services.AddInfrastructureServices(builder.Configuration);
        builder.Services.AddApplicationServices(builder.Configuration);
        builder.Services.AddApiServices(builder.Configuration);

        var app = builder.Build();
        app.UseExceptionHandler();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapNonaEndpoints();
        app.MapGet("/throw", ThrowForTest);

        await app.StartAsync();
        return app;
    }

    private static IResult ThrowForTest()
        => throw new InvalidOperationException("test exception");

    private static async Task<JsonDocument> ParseJsonAsync(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }

    private static async Task AssertProblemAsync(
        HttpResponseMessage response,
        JsonElement body,
        int status,
        string title)
    {
        await Assert.That((int)response.StatusCode).IsEqualTo(status);
        await Assert.That(response.Content.Headers.ContentType?.MediaType).IsEqualTo("application/problem+json");
        await Assert.That(body.GetProperty("status").GetInt32()).IsEqualTo(status);
        await Assert.That(body.GetProperty("title").GetString()).IsEqualTo(title);
        await Assert.That(body.GetProperty("type").GetString()).Contains("rfc9110");
        await Assert.That(body.GetProperty("instance").GetString()).IsEqualTo(response.RequestMessage?.RequestUri?.AbsolutePath);
    }
}
