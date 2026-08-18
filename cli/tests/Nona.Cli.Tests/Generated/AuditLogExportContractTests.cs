using System.Net;
using System.Text;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using CliApiClient = Nona.Cli.Generated.NonaApiClient;
using MigratorApiClient = Nona.Migrator.Core.Generated.NonaMigrationApiClient;

namespace Nona.Cli.Tests.Generated;

public sealed class AuditLogExportContractTests
{
    [Test]
    [Arguments("cli", "csv")]
    [Arguments("cli", "json")]
    [Arguments("migrator", "csv")]
    [Arguments("migrator", "json")]
    public async Task Export_SendsRequestedFormatAndReturnsReadableStream(
        string client,
        string format)
        => await AssertExportContractAsync(
            adapter => client switch
            {
                "cli" => new CliApiClient(adapter).Admin.AuditLogs.Export
                    .GetAsync(config => config.QueryParameters.Format = format),
                "migrator" => new MigratorApiClient(adapter).Admin.AuditLogs.Export
                    .GetAsync(config => config.QueryParameters.Format = format),
                _ => throw new ArgumentOutOfRangeException(nameof(client), client, null)
            },
            format);

    private static async Task AssertExportContractAsync(
        Func<HttpClientRequestAdapter, Task<Stream?>> sendAsync,
        string expectedFormat)
    {
        using var handler = new CaptureHandler();
        using var httpClient = new HttpClient(handler);
        using var adapter = new HttpClientRequestAdapter(
            new AnonymousAuthenticationProvider(),
            httpClient: httpClient);
        adapter.BaseUrl = "https://nona.example";

        await using Stream? stream = await sendAsync(adapter);

        await Assert.That(stream).IsNotNull();
        await Assert.That(stream!.CanRead).IsTrue();
        await Assert.That(handler.RequestUri).IsNotNull();
        await Assert.That(handler.RequestUri!.AbsolutePath).IsEqualTo("/admin/audit-logs/export");
        await Assert.That(handler.RequestUri.Query).IsEqualTo($"?format={expectedFormat}");
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        internal Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;

            var content = new StreamContent(
                new MemoryStream(Encoding.UTF8.GetBytes("actor,action\nadmin,export\n")));

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = content
            });
        }
    }
}
