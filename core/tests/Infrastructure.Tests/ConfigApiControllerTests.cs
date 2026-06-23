using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Nona.Application.Api.ConfigEntries.Queries;
using Nona.WebApi;
using Nona.WebApi.Endpoints;

namespace Nona.Infrastructure.Tests;

public class ConfigApiEndpointTests
{
    [Test]
    public async Task GetConfigValue_ReturnsRawValueBodyAndLogicalContentTypeHeader()
    {
        var mediator = new StubMediator(new GetConfigEntryValueResult(true, """{"enabled":true}""", "json", null));
        var httpContext = new DefaultHttpContext
        {
            Response =
            {
                Body = new MemoryStream()
            }
        };
        httpContext.RequestServices = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();

        var result = await NonaEndpointRouteBuilderExtensions.GetConfigValueAsync(
            "production",
            "features",
            httpContext,
            mediator,
            CancellationToken.None);

        await result.ExecuteAsync(httpContext);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(httpContext.Response.Body);
        var content = await reader.ReadToEndAsync();

        await Assert.That(content).IsEqualTo("""{"enabled":true}""");
        await Assert.That(httpContext.Response.ContentType).IsEqualTo("application/json");
        await Assert.That(httpContext.Response.Headers[NonaResponseHeaders.LogicalContentType].ToString()).IsEqualTo("json");
    }

    private sealed class StubMediator(GetConfigEntryValueResult result) : IMediator
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult((TResponse)(object)result);
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<object?>(result);
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest
        {
            return Task.CompletedTask;
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

        public Task Publish(object notification, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            return Task.CompletedTask;
        }
    }
}
