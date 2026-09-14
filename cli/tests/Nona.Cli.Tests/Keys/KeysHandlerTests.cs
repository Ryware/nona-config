using System.Net;
using Nona.Cli.Keys;
using Nona.Cli.Keys.Commands;
using Nona.Cli.Keys.Queries;
using static Nona.Cli.Tests.Fixtures;
using static Nona.Cli.Tests.TestHelpers;

#pragma warning disable TUnit0055

namespace Nona.Cli.Tests.Keys;

[NotInParallel]
public sealed class KeysHandlerTests
{
    private static readonly NonaCliConnectionOptions TestConnection = new("http://nona.test", "test-token");

    [Test]
    public async Task ShowKeysQueryHandler_ReturnsZero_WhenProjectFound()
    {
        var (result, output) = await CaptureOutputAsync(() =>
            new ShowKeysQueryHandler(MockHttp(HttpStatusCode.OK, ApiKeyArrayJson))
                .HandleAsync(new ShowKeysQuery(TestConnection, "my-project"), CancellationToken.None));

        await Assert.That(result).IsEqualTo(0);
        await Assert.That(output).Contains("Fingerprint: ••••••••90ABCDEF");
        await Assert.That(output).DoesNotContain(new string('A', 64));
    }

    [Test]
    public async Task ShowKeysQueryHandler_ReturnsOne_WhenProjectNotFound()
    {
        var result = await new ShowKeysQueryHandler(MockHttp(HttpStatusCode.NotFound, """{"error":"Project not found"}"""))
            .HandleAsync(new ShowKeysQuery(TestConnection, "missing-project"), CancellationToken.None);
        await Assert.That(result).IsEqualTo(1);
    }

    [Test]
    public async Task CreateApiKeyCommandHandler_ReturnsZero_OnSuccess()
    {
        var (result, output) = await CaptureOutputAsync(() =>
            new CreateApiKeyCommandHandler(MockHttp(HttpStatusCode.Created, CreatedApiKeyJson))
                .HandleAsync(new CreateApiKeyCommand(TestConnection, "my-project", "Web Client", "production", "client"), CancellationToken.None));

        await Assert.That(result).IsEqualTo(0);
        await Assert.That(output).Contains(new string('A', 64));
        await Assert.That(output).Contains("cannot be recovered");
    }

    [Test]
    public async Task DeleteApiKeyCommandHandler_ReturnsZero_OnSuccess()
    {
        HttpMethod? requestedMethod = null;
        string? requestedPath = null;
        var (result, output) = await CaptureOutputAsync(() =>
            new DeleteApiKeyCommandHandler(() => new HttpClient(new RecordingHandler(request =>
            {
                requestedMethod = request.Method;
                requestedPath = request.RequestUri?.AbsolutePath;
                return JsonResponse(HttpStatusCode.NoContent, string.Empty);
            })))
                .HandleAsync(new DeleteApiKeyCommand(TestConnection, "my-project", 7), CancellationToken.None));

        await Assert.That(result).IsEqualTo(0);
        await Assert.That(requestedMethod).IsEqualTo(HttpMethod.Delete);
        await Assert.That(requestedPath).IsEqualTo("/admin/projects/my-project/api-keys/7");
        await Assert.That(output).Contains("Deleted API key 7.");
    }

    [Test]
    public async Task KeysCommands_ExposeOnlyListCreateAndDelete()
    {
        var context = new CliContext(
            CliDefaults.Empty,
            null,
            new CliDefaultsStore("unused-defaults.json"),
            new CliSessionStore("unused-session.json"));

        var keys = new KeysCommands(context).Build();
        var commands = keys.Children
            .OfType<System.CommandLine.Command>()
            .Select(command => command.Name)
            .Order()
            .ToArray();

        await Assert.That(commands).IsEquivalentTo(["create", "delete", "list"]);
    }

    private static async Task<(int Result, string Output)> CaptureOutputAsync(Func<Task<int>> action)
    {
        var previousOut = Console.Out;
        using var output = new StringWriter();

        try
        {
            Console.SetOut(output);
            var result = await action();
            return (result, output.ToString());
        }
        finally
        {
            Console.SetOut(previousOut);
        }
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string body)
        => new(statusCode)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
        };

    private sealed class RecordingHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }
}
