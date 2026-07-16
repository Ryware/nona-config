using System.Net;
using System.Net.Http.Json;
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

public class AuthRegisterEndpointTests
{
    [Test]
    public async Task Register_ReturnsBareLoginResponse_AndUsesRegisterSpecificFailureStatuses()
    {
        await using var app = await StartAppAsync();
        var client = app.GetTestClient();
        var email = $"admin-{Guid.NewGuid():N}@example.com";

        using var registerResponse = await client.PostAsJsonAsync(
            "/auth/register",
            new { email, password = "Password123!" });
        using var registerBody = await ParseJsonAsync(registerResponse);

        await Assert.That(registerResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(registerBody.RootElement.TryGetProperty("token", out _)).IsTrue();
        await Assert.That(registerBody.RootElement.TryGetProperty("success", out _)).IsFalse();
        await Assert.That(registerBody.RootElement.TryGetProperty("response", out _)).IsFalse();
        await Assert.That(registerBody.RootElement.TryGetProperty("error", out _)).IsFalse();

        using var loginResponse = await client.PostAsJsonAsync(
            "/auth/login",
            new { email, password = "Password123!" });
        using var loginBody = await ParseJsonAsync(loginResponse);

        await Assert.That(loginResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(GetPropertyNames(registerBody.RootElement)).IsEquivalentTo(GetPropertyNames(loginBody.RootElement));

        using (var duplicateResponse = await client.PostAsJsonAsync(
            "/auth/register",
            new { email, password = "Password123!" }))
        {
            using var duplicateBody = await ParseJsonAsync(duplicateResponse);

            await Assert.That(duplicateResponse.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
            await Assert.That(duplicateBody.RootElement.GetProperty("error").GetString()).IsEqualTo("User already exists");
        }

        using (var closedResponse = await client.PostAsJsonAsync(
            "/auth/register",
            new { email = $"new-{Guid.NewGuid():N}@example.com", password = "Password123!" }))
        {
            using var closedBody = await ParseJsonAsync(closedResponse);

            await Assert.That(closedResponse.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
            await Assert.That(closedBody.RootElement.GetProperty("error").GetString()).IsEqualTo("Registration is disabled");
            await Assert.That(closedBody.RootElement.GetProperty("errorCode").GetString()).IsEqualTo("registration_disabled");
        }

        using (var invalidResponse = await client.PostAsJsonAsync(
            "/auth/register",
            new { email = "", password = "" }))
        {
            using var invalidBody = await ParseJsonAsync(invalidResponse);

            await Assert.That(invalidResponse.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
            await Assert.That(invalidBody.RootElement.GetProperty("error").GetString()).Contains("Email is required");
            await Assert.That(invalidBody.RootElement.GetProperty("error").GetString()).Contains("Password is required");
        }
    }

    [Test]
    public async Task OpenApi_RegisterResponseContract_DocumentsBareSuccessAndErrorStatuses()
    {
        await using var app = await StartAppAsync();
        var client = app.GetTestClient();

        using var response = await client.GetAsync("/openapi/v1.json");
        using var document = await ParseJsonAsync(response);

        var responses = document.RootElement
            .GetProperty("paths")
            .GetProperty("/auth/register")
            .GetProperty("post")
            .GetProperty("responses");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(responses.GetProperty("200").GetProperty("content").GetProperty("application/json").GetProperty("schema").GetProperty("$ref").GetString())
            .IsEqualTo("#/components/schemas/LoginResponse");
        await Assert.That(responses.TryGetProperty("400", out _)).IsTrue();
        await Assert.That(responses.TryGetProperty("403", out _)).IsTrue();
        await Assert.That(responses.TryGetProperty("409", out _)).IsTrue();
    }

    private static async Task<WebApplication> StartAppAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Storage:Type"] = "InMemory",
            ["Jwt:Key"] = "register-endpoint-tests-signing-key-1234567890",
            ["Jwt:Issuer"] = "register-endpoint-tests",
            ["Jwt:Audience"] = "register-endpoint-tests"
        });

        builder.Services.AddOpenApi();
        builder.Services.AddInfrastructureServices(builder.Configuration);
        builder.Services.AddApplicationServices(builder.Configuration);
        builder.Services.AddApiServices(builder.Configuration);

        var app = builder.Build();
        app.MapOpenApi();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapNonaEndpoints();

        await app.StartAsync();
        return app;
    }

    private static async Task<JsonDocument> ParseJsonAsync(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }

    private static IReadOnlyList<string> GetPropertyNames(JsonElement element)
    {
        return element.EnumerateObject()
            .Select(property => property.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }
}
