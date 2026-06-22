using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nona.Application.Api.ConfigEntries.Queries;
using Nona.WebApi;
using Nona.WebApi.Controllers.Api;

namespace Nona.Infrastructure.Tests;

public class ConfigApiControllerTests
{
    [Test]
    public async Task GetConfigValue_ReturnsRawValueBodyAndLogicalContentTypeHeader()
    {
        var mediator = new StubMediator(new GetConfigEntryValueResult(true, """{"enabled":true}""", "json", null));
        var controller = new ConfigController(mediator)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.GetConfigValue("production", "features", CancellationToken.None);

        var content = result as ContentResult;
        await Assert.That(content).IsNotNull();
        await Assert.That(content!.Content).IsEqualTo("""{"enabled":true}""");
        await Assert.That(content.ContentType).IsEqualTo("application/json");
        await Assert.That(controller.Response.Headers[NonaResponseHeaders.LogicalContentType].ToString()).IsEqualTo("json");
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
