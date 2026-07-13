using System.Net;
using System.Net.Http.Json;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nona.Application;
using Nona.Application.Auth.DTOs;
using Nona.Application.Common.Interfaces;
using Nona.WebApi.Endpoints;

namespace Nona.Infrastructure.Tests;

public class RequestValidationEndpointTests
{
    [Test]
    public async Task Login_ReturnsBadRequestAndDoesNotCallMediator_WhenRequestIsInvalid()
    {
        var mediator = new ThrowingMediator();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddApplicationServices(new ConfigurationBuilder().Build());
        builder.Services.AddSingleton<ISsoPublicConfigurationProvider>(new StubSsoPublicConfigurationProvider());
        builder.Services.RemoveAll<IMediator>();
        builder.Services.AddSingleton<IMediator>(mediator);

        await using var app = builder.Build();
        app.MapNonaEndpoints();
        await app.StartAsync();

        var response = await app.GetTestClient().PostAsJsonAsync("/auth/login", new LoginRequest("", ""));
        var body = await response.Content.ReadAsStringAsync();

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(body.Contains("Email is required", StringComparison.Ordinal)).IsTrue();
        await Assert.That(body.Contains("Password is required", StringComparison.Ordinal)).IsTrue();
        await Assert.That(mediator.SendCalls).IsEqualTo(0);
    }

    private sealed class StubSsoPublicConfigurationProvider : ISsoPublicConfigurationProvider
    {
        public SsoPublicConfigResponse GetConfiguration()
        {
            return new SsoPublicConfigResponse(
                new SsoProviderPublicConfig(false, null),
                new SsoProviderPublicConfig(false, null));
        }
    }

    private sealed class ThrowingMediator : IMediator
    {
        public int SendCalls { get; private set; }

        public ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            SendCalls++;
            throw new InvalidOperationException("Mediator should not be called for invalid requests.");
        }

        public ValueTask<TResponse> Send<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default)
        {
            SendCalls++;
            throw new InvalidOperationException("Mediator should not be called for invalid requests.");
        }

        public ValueTask<TResponse> Send<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default)
        {
            SendCalls++;
            throw new InvalidOperationException("Mediator should not be called for invalid requests.");
        }

        public ValueTask<object?> Send(object message, CancellationToken cancellationToken = default)
        {
            SendCalls++;
            throw new InvalidOperationException("Mediator should not be called for invalid requests.");
        }

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamQuery<TResponse> query,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamCommand<TResponse> command,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask Publish(object notification, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            throw new NotSupportedException();
        }
    }
}
