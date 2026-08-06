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
    [Arguments("cli", "csv", "text/csv")]
    [Arguments("cli", "json", "application/json")]
    [Arguments("migrator", "csv", "text/csv")]
    [Arguments("migrator", "json", "application/json")]
    public async Task Export_AdvertisesRequestedSuccessMediaTypeAndReturnsReadableStream(
        string client,
        string format,
        string expectedMediaType)
        => await AssertExportContractAsync(
            adapter => client switch
            {
                "cli" => new CliExportRequestBuilder(
                        "https://nona.example/admin/audit-logs/export",
                        adapter)
                    .GetAsync(config => config.QueryParameters.Format = format),
                "migrator" => new MigratorExportRequestBuilder(
                        "https://nona.example/admin/audit-logs/export",
                        adapter)
                    .GetAsync(config => config.QueryParameters.Format = format),
                _ => throw new ArgumentOutOfRangeException(nameof(client), client, null)
            },
            expectedMediaType);

    private static async Task AssertExportContractAsync(
        Func<HttpClientRequestAdapter, Task<Stream?>> sendAsync,
        string expectedMediaType)
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
        await Assert.That(acceptMediaTypes).Contains(expectedMediaType);
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
