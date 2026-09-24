using System.Net;
using System.Text;
using Nona.Cli.Entries;

namespace Nona.Cli.Tests.Entries;

public sealed class AdminReleaseEntryReaderTests
{
    private static readonly NonaCliConnectionOptions TestConnection = new("http://nona.test", "test-token");

    private const string EnvironmentWithActiveReleaseJson = """
        [{"name":"production","project":"my-project","activeReleaseVersion":"1.2.0","createdAt":"2024-01-01T00:00:00Z","updatedAt":"2024-01-01T00:00:00Z"}]
        """;

    private const string EnvironmentWithoutActiveReleaseJson = """
        [{"name":"production","project":"my-project","activeReleaseVersion":null,"createdAt":"2024-01-01T00:00:00Z","updatedAt":"2024-01-01T00:00:00Z"}]
        """;

    private const string ReleaseDetailsJson = """
        {
          "project": "my-project",
          "environment": "production",
          "version": "1.2.0",
          "entryCount": 1,
          "isActive": true,
          "createdAt": "2024-01-01T00:00:00Z",
          "actor": "alice",
          "entries": [
            {"key": "feature.checkout", "value": "true", "contentType": "boolean", "scope": "all"}
          ]
        }
        """;

    [Test]
    public async Task ReadAsync_ReturnsEntries_ForExactVersion()
    {
        var api = NonaClientFactory.Create(TestConnection, CreateHttpFactory((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, ReleaseDetailsJson))));

        var result = await AdminReleaseEntryReader.ReadAsync(api, "my-project", "production", "1.2.0", CancellationToken.None);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.ResolvedVersion).IsEqualTo("1.2.0");
        await Assert.That(result.Entries!.Single().Key).IsEqualTo("feature.checkout");
    }

    [Test]
    public async Task ReadAsync_ResolvesActiveVersion_WhenNoVersionGiven()
    {
        var requestNumber = 0;
        var api = NonaClientFactory.Create(TestConnection, CreateHttpFactory((_, _) =>
        {
            requestNumber++;
            return Task.FromResult(requestNumber switch
            {
                1 => JsonResponse(HttpStatusCode.OK, EnvironmentWithActiveReleaseJson),
                2 => JsonResponse(HttpStatusCode.OK, ReleaseDetailsJson),
                _ => throw new InvalidOperationException("Unexpected request")
            });
        }));

        var result = await AdminReleaseEntryReader.ReadAsync(api, "my-project", "production", null, CancellationToken.None);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.ResolvedVersion).IsEqualTo("1.2.0");
    }

    [Test]
    public async Task ReadAsync_ReturnsFailure_WhenNoActiveReleaseConfigured()
    {
        var api = NonaClientFactory.Create(TestConnection, CreateHttpFactory((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.OK, EnvironmentWithoutActiveReleaseJson))));

        var result = await AdminReleaseEntryReader.ReadAsync(api, "my-project", "production", null, CancellationToken.None);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).Contains("No active release");
    }

    [Test]
    public async Task ReadAsync_ReturnsFailure_WhenReleaseNotFound()
    {
        var api = NonaClientFactory.Create(TestConnection, CreateHttpFactory((_, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.NotFound, string.Empty))));

        var result = await AdminReleaseEntryReader.ReadAsync(api, "my-project", "production", "9.9.9", CancellationToken.None);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).Contains("not found");
    }

    private static Func<HttpClient> CreateHttpFactory(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
        => () => new HttpClient(new StubHttpMessageHandler(responder));

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string body)
        => new(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => responder(request, cancellationToken);
    }
}
