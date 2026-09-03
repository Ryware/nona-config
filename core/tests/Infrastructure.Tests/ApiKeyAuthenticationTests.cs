using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Nona.Application.Common;
using Nona.Application.Common.Interfaces;
using Nona.WebApi.Authentication;
using Nona.WebApi.Services;

namespace Nona.Infrastructure.Tests;

public class ApiKeyAuthenticationTests
{
    private const string GenericUnauthorizedDetail = "An API key is required or invalid.";

    [Test]
    public async Task Authentication_ExposesOnlyTheApiKeyHash()
    {
        const string secret = "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";
        await using var app = await StartAppAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api-key-hash");
        request.Headers.Add(ApiKeyAuthenticationHandler.ApiKeyHeaderName, secret);

        using var response = await app.GetTestClient().SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(body).IsEqualTo(ApiKeySecret.Hash(secret));
        await Assert.That(body).IsNotEqualTo(secret);
    }

    [Test]
    public async Task Authentication_RejectsMalformedApiKeysWithGenericProblemDetails()
    {
        var malformedKeys = new[]
        {
            string.Empty,
            " ",
            new string('A', 63),
            new string('A', 65),
            new string('Z', 64),
            new string('a', 64),
            new string('\u00e9', 64)
        };

        await using var app = await StartAppAsync();
        var client = app.GetTestClient();

        using (var missingResponse = await client.GetAsync("/api-key-hash"))
        {
            await AssertGenericUnauthorizedAsync(missingResponse);
        }

        foreach (var malformedKey in malformedKeys)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api-key-hash");
            request.Headers.TryAddWithoutValidation(ApiKeyAuthenticationHandler.ApiKeyHeaderName, malformedKey);
            using var response = await client.SendAsync(request);

            await AssertGenericUnauthorizedAsync(response);
        }

        using var multipleRequest = new HttpRequestMessage(HttpMethod.Get, "/api-key-hash");
        multipleRequest.Headers.TryAddWithoutValidation(
            ApiKeyAuthenticationHandler.ApiKeyHeaderName,
            [new string('A', 64), new string('B', 64)]);
        using var multipleResponse = await client.SendAsync(multipleRequest);
        await AssertGenericUnauthorizedAsync(multipleResponse);
    }

    private static async Task AssertGenericUnauthorizedAsync(HttpResponseMessage response)
    {
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        await Assert.That(response.Content.Headers.ContentType?.MediaType)
            .IsEqualTo("application/problem+json");

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var body = await JsonDocument.ParseAsync(stream);
        await Assert.That(body.RootElement.GetProperty("title").GetString()).IsEqualTo("Unauthorized");
        await Assert.That(body.RootElement.GetProperty("status").GetInt32()).IsEqualTo(401);
        await Assert.That(body.RootElement.GetProperty("detail").GetString())
            .IsEqualTo(GenericUnauthorizedDetail);
    }

    private static async Task<WebApplication> StartAppAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<IApiKeyService, ApiKeyService>();
        builder.Services
            .AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
                ApiKeyAuthenticationHandler.SchemeName,
                null);
        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(ApiKeyAuthenticationHandler.SchemeName, policy => policy
                .AddAuthenticationSchemes(ApiKeyAuthenticationHandler.SchemeName)
                .RequireAuthenticatedUser());

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGet(
                "/api-key-hash",
                (IApiKeyService apiKeyService) => Results.Text(apiKeyService.GetCurrentApiKeyHash()))
            .RequireAuthorization(ApiKeyAuthenticationHandler.SchemeName);

        await app.StartAsync();
        return app;
    }
}
