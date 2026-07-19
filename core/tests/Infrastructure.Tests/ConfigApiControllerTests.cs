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
            null,
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

    [Test]
    public async Task GetAllConfigValues_ReturnsMapAndEtag()
    {
        var values = new Dictionary<string, ClientConfigValueDto>
        {
            ["Features:Checkout"] = new("true", "boolean")
        };
        var mediator = new StubMediator(new GetAllConfigValuesResult(
            true,
            values,
            null,
            "\"release-1\""));
        var httpContext = CreateHttpContext();

        var result = await NonaEndpointRouteBuilderExtensions.GetAllConfigValuesAsync(
            "production",
            null,
            httpContext,
            mediator,
            CancellationToken.None);

        await result.ExecuteAsync(httpContext);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(httpContext.Response.Body);
        var content = await reader.ReadToEndAsync();

        await Assert.That(httpContext.Response.StatusCode).IsEqualTo(StatusCodes.Status200OK);
        await Assert.That(content).Contains("Features:Checkout");
        await Assert.That(content).Contains("contentType");
        await Assert.That(httpContext.Response.Headers.ETag.ToString()).StartsWith("\"");
        await Assert.That(httpContext.Response.Headers.CacheControl.ToString()).IsEqualTo("private, no-cache");
    }

    [Test]
    public async Task GetAllConfigValues_ReturnsNotModifiedForMatchingEtag()
    {
        const string etag = "\"release-1\"";
        var mediator = new StubMediator(new GetAllConfigValuesResult(
            true,
            null,
            null,
            etag,
            true));
        var httpContext = CreateHttpContext();
        httpContext.Request.Headers.IfNoneMatch = etag;
        var result = await NonaEndpointRouteBuilderExtensions.GetAllConfigValuesAsync(
            "production",
            null,
            httpContext,
            mediator,
            CancellationToken.None);
        await result.ExecuteAsync(httpContext);

        await Assert.That(httpContext.Response.StatusCode).IsEqualTo(StatusCodes.Status304NotModified);
        await Assert.That(httpContext.Response.Headers.ETag.ToString()).IsEqualTo(etag);
        await Assert.That(httpContext.Response.Body.Length).IsEqualTo(0);
    }

    [Test]
    public async Task GetAllConfigValues_MasksUnreadableScopeAsNotFound()
    {
        var mediator = new StubMediator(new GetAllConfigValuesResult(
            false,
            null,
            "Environment not found"));
        var httpContext = CreateHttpContext();

        var result = await NonaEndpointRouteBuilderExtensions.GetAllConfigValuesAsync(
            "production",
            null,
            httpContext,
            mediator,
            CancellationToken.None);
        await result.ExecuteAsync(httpContext);

        await Assert.That(httpContext.Response.StatusCode).IsEqualTo(StatusCodes.Status404NotFound);
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext
        {
            Response =
            {
                Body = new MemoryStream()
            }
        };
        context.RequestServices = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();
        return context;
    }

    private sealed class StubMediator(object result) : IMediator
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
