using System.Net;
using System.Text;
using System.Text.Json;
using Nona.Cli.Releases.Commands;

#pragma warning disable TUnit0055

namespace Nona.Cli.Tests.Releases;

[NotInParallel]
public sealed class AmendReleaseCommandHandlerTests
{
    private static readonly NonaCliConnectionOptions TestConnection =
        new("http://nona.test", "test-token");

    [Test]
    public async Task HandleAsync_PrintsEveryValidationErrorWithoutRetryingOrChangingEntries()
    {
        const string sourceJson = """
            {
              "project": "my-project",
              "environment": "production",
              "version": "1.1.0",
              "entryCount": 5,
              "isActive": false,
              "createdAt": "2024-01-01T00:00:00Z",
              "actor": "alice",
              "entries": [
                {"key": "legacy key", "value": "kept", "contentType": "text", "scope": "all"},
                {"key": "DUPLICATE", "value": "first", "contentType": "text", "scope": "client"},
                {"key": "duplicate", "value": "second", "contentType": "text", "scope": "server"},
                {"key": "legacy.scope", "value": "kept", "contentType": "text", "scope": "public"},
                {"key": "legacy.value", "value": "not-a-number", "contentType": "number", "scope": "all"}
              ]
            }
            """;
        const string validationProblemJson = """
            {
              "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
              "title": "One or more validation errors occurred.",
              "status": 400,
              "detail": "One or more validation errors occurred.",
              "instance": "/admin/projects/my-project/environments/production/releases",
              "errors": {
                "Entries[0].Key": ["Release entry keys may only contain letters, numbers, colons, underscores, periods, and hyphens."],
                "Entries[2].Key": ["Release entry keys must be unique (case-insensitive)."],
                "Entries[3].Scope": ["Invalid scope. Must be 'client', 'server', or 'all'."],
                "Entries[4].ContentType": ["Content type must be one of: text, number, boolean, json."],
                "Entries[4].Value": ["Value must be a valid number."]
              }
            }
            """;

        var postCount = 0;
        string? postedBody = null;
        var factory = CreateHttpFactory(async request =>
        {
            if (request.Method == HttpMethod.Get &&
                request.RequestUri?.AbsolutePath ==
                "/admin/projects/my-project/environments/production/releases/1.1.0")
            {
                return JsonResponse(HttpStatusCode.OK, sourceJson);
            }

            if (request.Method == HttpMethod.Post &&
                request.RequestUri?.AbsolutePath ==
                "/admin/projects/my-project/environments/production/releases")
            {
                postCount++;
                postedBody = await request.Content!.ReadAsStringAsync();
                return ProblemResponse(HttpStatusCode.BadRequest, validationProblemJson);
            }

            throw new InvalidOperationException(
                $"Unexpected request: {request.Method} {request.RequestUri}");
        });

        var (exitCode, output, error) = await CaptureOutputAsync(() =>
            new AmendReleaseCommandHandler(factory).HandleAsync(
                new AmendReleaseCommand(
                    TestConnection,
                    "my-project",
                    "production",
                    "1.1.0",
                    "1.1.1"),
                CancellationToken.None));

        await Assert.That(exitCode).IsEqualTo(CliExitCodes.ValidationError);
        await Assert.That(postCount).IsEqualTo(1);
        await Assert.That(output).IsEmpty();
        await Assert.That(output).DoesNotContain("Published");
        await Assert.That(error).Contains(
            "Entries[0].Key: Release entry keys may only contain letters, numbers, colons, underscores, periods, and hyphens.");
        await Assert.That(error).Contains(
            "Entries[2].Key: Release entry keys must be unique (case-insensitive).");
        await Assert.That(error).Contains(
            "Entries[3].Scope: Invalid scope. Must be 'client', 'server', or 'all'.");
        await Assert.That(error).Contains(
            "Entries[4].ContentType: Content type must be one of: text, number, boolean, json.");
        await Assert.That(error).Contains(
            "Entries[4].Value: Value must be a valid number.");

        using var source = JsonDocument.Parse(sourceJson);
        using var posted = JsonDocument.Parse(postedBody!);
        await Assert.That(posted.RootElement.GetProperty("version").GetString()).IsEqualTo("1.1.1");
        await Assert.That(posted.RootElement.GetProperty("makeActive").GetBoolean()).IsFalse();
        await Assert.That(JsonElement.DeepEquals(
            posted.RootElement.GetProperty("entries"),
            source.RootElement.GetProperty("entries"))).IsTrue();
    }

    private static Func<HttpClient> CreateHttpFactory(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> responder)
        => () => new HttpClient(new StubHttpMessageHandler(responder));

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string body)
        => new(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

    private static HttpResponseMessage ProblemResponse(HttpStatusCode statusCode, string body)
        => new(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/problem+json")
        };

    private static async Task<(int ExitCode, string Output, string Error)> CaptureOutputAsync(
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
        Func<HttpRequestMessage, Task<HttpResponseMessage>> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => responder(request);
    }
}
