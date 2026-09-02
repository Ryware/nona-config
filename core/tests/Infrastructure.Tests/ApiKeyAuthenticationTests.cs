using System.Net;
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
