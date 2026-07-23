using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nona.Application;
using Nona.Infrastructure;
using Nona.WebApi;
using Nona.WebApi.Endpoints;

namespace Nona.Infrastructure.Tests;

public class SpaFallbackEndpointTests
{
    private const string SpaMarker = "<html><body>nona-spa-test</body></html>";

    [Test]
    [Arguments("/api", 404, "Not Found")]
    [Arguments("/api/production/key/extra", 404, "Not Found")]
    [Arguments("/api/static-test.html", 401, "Unauthorized")]
    [Arguments("/api/doesnotexist/whatever", 401, "Unauthorized")]
    [Arguments("/admin/not-a-route", 401, "Unauthorized")]
    [Arguments("/auth/not-a-route", 404, "Not Found")]
    [Arguments("/public/not-a-route", 404, "Not Found")]
    public async Task BackendPaths_NeverReachSpaFallback(
        string path,
        int expectedStatus,
        string expectedTitle)
    {
        await using var fixture = await StartAppAsync();

        using var response = await fixture.App.GetTestClient().GetAsync(path);
        using var body = await ParseJsonAsync(response);

        await Assert.That((int)response.StatusCode).IsEqualTo(expectedStatus);
        await Assert.That(response.Content.Headers.ContentType?.MediaType).IsEqualTo("application/problem+json");
        await Assert.That(body.RootElement.GetProperty("status").GetInt32()).IsEqualTo(expectedStatus);
        await Assert.That(body.RootElement.GetProperty("title").GetString()).IsEqualTo(expectedTitle);
        await Assert.That(body.RootElement.GetRawText()).DoesNotContain(SpaMarker);
    }

    [Test]
    [Arguments("/")]
    [Arguments("/dashboard")]
    [Arguments("/projects/example/deep-link")]
    [Arguments("/apiary")]
    public async Task FrontendPaths_StillServeSpa(string path)
    {
        await using var fixture = await StartAppAsync();

        using var response = await fixture.App.GetTestClient().GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(response.Content.Headers.ContentType?.MediaType).IsEqualTo("text/html");
        await Assert.That(body).IsEqualTo(SpaMarker);
    }

    private static async Task<TestApp> StartAppAsync()
    {
        var webRoot = Path.Combine(Path.GetTempPath(), $"nona-spa-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(webRoot, "api"));
        await File.WriteAllTextAsync(Path.Combine(webRoot, "index.html"), SpaMarker);
        await File.WriteAllTextAsync(Path.Combine(webRoot, "api", "static-test.html"), "backend-static-leak");

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            WebRootPath = webRoot
        });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Storage:Type"] = "InMemory",
            ["Jwt:Key"] = "spa-fallback-tests-signing-key-1234567890",
            ["Jwt:Issuer"] = "spa-fallback-tests",
            ["Jwt:Audience"] = "spa-fallback-tests"
        });

        builder.Services.AddInfrastructureServices(builder.Configuration);
        builder.Services.AddApplicationServices(builder.Configuration);
        builder.Services.AddApiServices(builder.Configuration);

        var app = builder.Build();
        app.UseExceptionHandler();
        app.UseNonaSpaStaticFiles();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapNonaEndpoints();
        app.MapNonaBackendFallbacks();
        app.MapFallbackToFile("index.html");

        await app.StartAsync();
        return new TestApp(app, webRoot);
    }

    private static async Task<JsonDocument> ParseJsonAsync(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }

    private sealed class TestApp(WebApplication app, string webRoot) : IAsyncDisposable
    {
        public WebApplication App { get; } = app;

        public async ValueTask DisposeAsync()
        {
            await App.DisposeAsync();
            Directory.Delete(webRoot, recursive: true);
        }
    }
}
