using Mediator;
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
        public ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult((TResponse)(object)result);
        }

        public ValueTask<TResponse> Send<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<TResponse> Send<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<object?> Send(object message, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<object?>(result);
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
            return ValueTask.CompletedTask;
        }

        public ValueTask Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            return ValueTask.CompletedTask;
        }
    }
}
