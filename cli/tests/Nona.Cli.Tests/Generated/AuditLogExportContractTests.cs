using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using CliExportRequestBuilder = Nona.Cli.Generated.Admin.AuditLogs.Export.ExportRequestBuilder;
using MigratorExportRequestBuilder = Nona.Migrator.Core.Generated.Admin.AuditLogs.Export.ExportRequestBuilder;

namespace Nona.Cli.Tests.Generated;

public sealed class AuditLogExportContractTests
{
    [Test]
    public async Task CliExport_AdvertisesSuccessMediaTypeAndReturnsReadableStream()
        => await AssertExportContractAsync(adapter =>
            new CliExportRequestBuilder(
                    "https://nona.example/admin/audit-logs/export",
                    adapter)
                .GetAsync(config => config.QueryParameters.Format = "csv"));

    [Test]
    public async Task MigratorExport_AdvertisesSuccessMediaTypeAndReturnsReadableStream()
        => await AssertExportContractAsync(adapter =>
            new MigratorExportRequestBuilder(
                    "https://nona.example/admin/audit-logs/export",
                    adapter)
                .GetAsync(config => config.QueryParameters.Format = "csv"));

    private static async Task AssertExportContractAsync(
        Func<HttpClientRequestAdapter, Task<Stream?>> sendAsync)
    {
        using var handler = new CaptureHandler();
        using var httpClient = new HttpClient(handler);
        using var adapter = new HttpClientRequestAdapter(
            new AnonymousAuthenticationProvider(),
            httpClient: httpClient);

        await using Stream? stream = await sendAsync(adapter);

        await Assert.That(stream).IsNotNull();
        await Assert.That(stream!.CanRead).IsTrue();
        await Assert.That(handler.AcceptMediaTypes).IsNotNull();

        var acceptMediaTypes = handler.AcceptMediaTypes!;
        var advertisesSuccess = acceptMediaTypes.Contains("text/csv", StringComparer.OrdinalIgnoreCase)
            || acceptMediaTypes.Contains("application/json", StringComparer.OrdinalIgnoreCase);
        var advertisesOnlyProblem = acceptMediaTypes.Count == 1
            && acceptMediaTypes.Contains("application/problem+json", StringComparer.OrdinalIgnoreCase);

        await Assert.That(advertisesSuccess).IsTrue();
        await Assert.That(advertisesOnlyProblem).IsFalse();
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        internal IReadOnlyList<string>? AcceptMediaTypes { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            AcceptMediaTypes = request.Headers.Accept
                .Select(value => value.MediaType)
                .OfType<string>()
                .ToArray();

            var content = new StreamContent(
                new MemoryStream(Encoding.UTF8.GetBytes("actor,action\nadmin,export\n")));
            content.Headers.ContentType = new MediaTypeHeaderValue("text/csv");

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = content
            });
        }
    }
}
