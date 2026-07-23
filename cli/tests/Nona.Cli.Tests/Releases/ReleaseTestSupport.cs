using System.Net;
using System.Text;

#pragma warning disable TUnit0055

namespace Nona.Cli.Tests.Releases;

internal static class ReleaseTestSupport
{
    internal static readonly NonaCliConnectionOptions Connection =
        new("http://nona.test", "test-token");

    internal const string ReleaseListJson = """
        [
          {
            "project": "my-project",
            "environment": "production",
            "version": "1.2.0",
            "entryCount": 2,
            "isActive": true,
            "createdAt": "2024-01-02T00:00:00Z",
            "actor": "alice"
          },
          {
            "project": "my-project",
            "environment": "production",
            "version": "1.1.0",
            "entryCount": 1,
            "isActive": false,
            "createdAt": "2024-01-01T00:00:00Z",
            "actor": "bob"
          }
        ]
        """;

    internal const string ReleaseDetailsJson = """
        {
          "project": "my-project",
          "environment": "production",
          "version": "1.2.0",
          "entryCount": 2,
          "isActive": true,
          "createdAt": "2024-01-02T00:00:00Z",
          "actor": "alice",
          "entries": [
            {"key": "feature.checkout", "value": "true", "contentType": "boolean", "scope": "all"},
            {"key": "api.url", "value": "https://api.example.com", "contentType": "text", "scope": "server"}
          ]
        }
        """;

    internal static Func<HttpClient> CreateHttpFactory(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
        => () => new HttpClient(new StubHttpMessageHandler(responder));

    internal static Func<HttpClient> CreateHttpFactory(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> responder)
        => CreateHttpFactory((request, _) => responder(request));

    internal static HttpResponseMessage JsonResponse(
        HttpStatusCode statusCode,
        string body)
        => new(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

    internal static HttpResponseMessage ProblemResponse(
        HttpStatusCode statusCode,
        string body)
        => new(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/problem+json")
        };

    internal static async Task<(int ExitCode, string Output, string Error)> CaptureOutputAsync(
        Func<Task<int>> action)
    {
        var previousOut = Console.Out;
        var previousError = Console.Error;
        using var output = new StringWriter();
        using var error = new StringWriter();

        try
        {
            Console.SetOut(output);
            Console.SetError(error);
            var exitCode = await action();
            return (exitCode, output.ToString(), error.ToString());
        }
        finally
        {
            Console.SetOut(previousOut);
            Console.SetError(previousError);
        }
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => responder(request, cancellationToken);
    }
}
