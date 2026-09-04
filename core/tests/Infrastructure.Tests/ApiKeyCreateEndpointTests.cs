using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Mediator;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nona.Application;
using Nona.Application.Admin.ApiKeys.Commands;
using Nona.Application.Admin.ApiKeys.DTOs;
using Nona.Application.Auth.DTOs;
using Nona.Application.Common.Interfaces;
using Nona.WebApi.Endpoints;

namespace Nona.Infrastructure.Tests;

public class ApiKeyCreateEndpointTests
{
    [Test]
    public async Task Create_WhenSuccessful_PreventsSecretCaching()
    {
        var createdApiKey = CreateCreatedApiKeyDto();
        await using var app = await StartAppAsync(new CreateApiKeyResult(true, createdApiKey, null));

        using var response = await app.GetTestClient().PostAsJsonAsync(
            "/admin/projects/alpha/api-keys/",
            new CreateApiKeyRequest("deployment"));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Created);
        await Assert.That(response.Headers.CacheControl?.NoStore).IsTrue();
    }

    [Test]
    public async Task OpenApi_DoesNotExposeRegenerateEndpoint()
    {
        await using var app = await StartAppAsync();

        using var response = await app.GetTestClient().GetAsync("/openapi/v1.json");
        using var body = await ParseJsonAsync(response);
        var exposesRegenerate = body.RootElement
            .GetProperty("paths")
            .TryGetProperty("/admin/projects/{projectId}/api-keys/{apiKeyId}/regenerate", out _);

        await Assert.That(exposesRegenerate).IsFalse();
    }

    private static CreatedApiKeyDto CreateCreatedApiKeyDto()
    {
        var timestamp = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        return new CreatedApiKeyDto(
            7,
            "deployment",
            new string('a', 64),
            "aaaaaaaa",
            "alpha",
            null,
            "client",
            timestamp,
            timestamp);
    }

    private static async Task<WebApplication> StartAppAsync(object? mediatorResult = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddOpenApi();
        builder.Services.AddProblemDetails();
        builder.Services.AddApplicationServices(new ConfigurationBuilder().Build());
        builder.Services.AddSingleton<ISsoPublicConfigurationProvider>(new StubSsoPublicConfigurationProvider());
        builder.Services.RemoveAll<IMediator>();
        builder.Services.AddSingleton<IMediator>(new StubMediator(
            mediatorResult ?? new CreateApiKeyResult(false, null, "API key could not be created")));
        builder.Services
            .AddAuthentication("Test")
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", null);
        builder.Services.AddAuthorization();

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

    private sealed class StubSsoPublicConfigurationProvider : ISsoPublicConfigurationProvider
    {
        public SsoPublicConfigResponse GetConfiguration()
            => new(new SsoProviderPublicConfig(false, null), new SsoProviderPublicConfig(false, null));
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "admin")], Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }

    private sealed class StubMediator(object result) : IMediator
    {
        public ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
            => ValueTask.FromResult((TResponse)result);

        public ValueTask<TResponse> Send<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public ValueTask<TResponse> Send<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public ValueTask<object?> Send(object message, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<object?>(result);

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamQuery<TResponse> query, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamCommand<TResponse> command, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public ValueTask Publish(object notification, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
            => ValueTask.CompletedTask;
    }
}
