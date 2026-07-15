using System.Net;
using System.Text;
using System.Text.Json;
using Nona.Cli.Init.Commands;

#pragma warning disable TUnit0055

namespace Nona.Cli.Tests.Init;

[NotInParallel]
public sealed class InitCommandHandlerTests
{
    private const string FullKey = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA158D";

    [Test]
    public async Task ColdStart_BootstrapsResources_AndPrintsDotenv()
    {
        var server = new SequenceServer(
            Json(HttpStatusCode.OK, "true"),
            Json(HttpStatusCode.OK, """
                {"success":true,"response":{"token":"jwt-token","username":"admin@example.com","role":"viewer","expiresAt":"2026-06-04T12:00:00Z"},"error":null}
                """),
            Json(HttpStatusCode.OK, "[]"),
            Json(HttpStatusCode.Created, """{"name":"nona-todo"}"""),
            Json(HttpStatusCode.OK, "[]"),
            Json(HttpStatusCode.Created, """{"name":"production"}"""),
            Json(HttpStatusCode.OK, """{"key":"Features:Example"}"""),
            Json(HttpStatusCode.OK, "[]"),
            Json(HttpStatusCode.Created, $$"""{"id":7,"name":"nona init client","key":"{{FullKey}}","environment":"production","scope":"client"}"""));

        var (exitCode, output) = await RunAsync(server, PrintKey: false);

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains("VITE_NONA_BASE_URL=http://nona.test");
        await Assert.That(output).Contains("VITE_NONA_API_KEY=****158D");
        await Assert.That(output).Contains("--print-key");
        await Assert.That(server.Requests.Select(r => $"{r.Method} {r.Path}")).IsEquivalentTo([
            "GET /auth/first-time",
            "POST /auth/register",
            "GET /admin/projects",
            "POST /admin/projects",
            "GET /admin/projects/nona-todo/environments",
            "POST /admin/projects/nona-todo/environments",
            "PUT /admin/projects/nona-todo/environments/production/config-entries/Features%3AExample",
            "GET /admin/projects/nona-todo/api-keys",
            "POST /admin/projects/nona-todo/api-keys"
        ]);
    }

    [Test]
    public async Task WarmStart_ReusesExistingProjectEnvironmentAndKey()
    {
        var server = new SequenceServer(
            Json(HttpStatusCode.OK, "false"),
            Json(HttpStatusCode.OK, """{"token":"jwt-token","username":"admin@example.com","role":"viewer","expiresAt":"2026-06-04T12:00:00Z"}"""),
            Json(HttpStatusCode.OK, """[{"name":"nona-todo"}]"""),
            Json(HttpStatusCode.OK, """[{"name":"production"}]"""),
            Json(HttpStatusCode.OK, """{"key":"Features:Example"}"""),
            Json(HttpStatusCode.OK, $$"""[{"id":7,"name":"nona init client","key":"{{FullKey}}","environment":"production","scope":"client"}]"""));

        var (exitCode, output) = await RunAsync(server, PrintKey: true);

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(output).Contains($"VITE_NONA_API_KEY={FullKey}");
        await Assert.That(server.Requests.Any(r => r.Method == "POST" && r.Path.Contains("api-keys", StringComparison.Ordinal))).IsFalse();
        await Assert.That(server.Requests.Any(r => r.Method == "POST" && r.Path == "/admin/projects")).IsFalse();
    }

    [Test]
    public async Task JsonFormat_EmitsMachineParseableOutput()
    {
        var server = new SequenceServer(
            Json(HttpStatusCode.OK, "false"),
            Json(HttpStatusCode.OK, """{"token":"jwt-token"}"""),
            Json(HttpStatusCode.OK, """[{"name":"nona-todo"}]"""),
            Json(HttpStatusCode.OK, """[{"name":"production"}]"""),
            Json(HttpStatusCode.OK, "[]"),
            Json(HttpStatusCode.Created, $$"""{"id":7,"name":"nona init client","key":"{{FullKey}}","environment":"production","scope":"client"}"""));

        var command = BuildCommand(IncludeSeedFlag: false, Format: "json", PrintKey: true);
        var (exitCode, output) = await RunAsync(server, command);

        using var document = JsonDocument.Parse(output);
        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(document.RootElement.GetProperty("apiKey").GetString()).IsEqualTo(FullKey);
        await Assert.That(document.RootElement.GetProperty("seededFlag").ValueKind).IsEqualTo(JsonValueKind.Null);
    }

    [Test]
    public async Task BadCredentials_ReturnsThree()
    {
        var server = new SequenceServer(
            Json(HttpStatusCode.OK, "false"),
            Json(HttpStatusCode.Unauthorized, """{"error":"Invalid username or password"}"""));

        var (exitCode, _) = await RunAsync(server);

        await Assert.That(exitCode).IsEqualTo(3);
    }

    [Test]
    public async Task UnreachableBaseUrl_ReturnsFour()
    {
        var handler = new InitCommandHandler(() => new HttpClient(new ThrowingHandler(), disposeHandler: false));

        var exitCode = await handler.HandleAsync(BuildCommand(), CancellationToken.None);

        await Assert.That(exitCode).IsEqualTo(4);
    }

    private static async Task<(int ExitCode, string Output)> RunAsync(
        SequenceServer server,
        bool PrintKey = true)
    {
        return await RunAsync(server, BuildCommand(PrintKey: PrintKey));
    }

    private static async Task<(int ExitCode, string Output)> RunAsync(
        SequenceServer server,
        InitCommand command)
    {
        var previousOut = Console.Out;
        var previousError = Console.Error;
        using var output = new StringWriter();
        using var error = new StringWriter();

        try
        {
            Console.SetOut(output);
            Console.SetError(error);

            var handler = new InitCommandHandler(server.CreateClient);
            var exitCode = await handler.HandleAsync(command, CancellationToken.None);
            return (exitCode, output.ToString());
        }
        finally
        {
            Console.SetOut(previousOut);
            Console.SetError(previousError);
        }
    }

    private static InitCommand BuildCommand(
        SeedFlag? SeedFlag = null,
        string Format = "dotenv",
        bool PrintKey = true,
        bool IncludeSeedFlag = true)
    {
        return new InitCommand(
            BaseUrl: "http://nona.test",
            Email: "admin@example.com",
            Password: "Password123!",
            Project: "nona-todo",
            Environment: "production",
            SeedFlag: IncludeSeedFlag ? SeedFlag ?? new SeedFlag("Features:Example", "true") : null,
            Scope: "client",
            Format: Format,
            PrintKey: PrintKey);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body)
    {
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
    }

    private sealed class SequenceServer(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);
        public List<RecordedRequest> Requests { get; } = [];

        public HttpClient CreateClient() => new(this, disposeHandler: false);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            Requests.Add(new RecordedRequest(request.Method.Method, request.RequestUri!.AbsolutePath, body));

            if (_responses.Count == 0)
                return Json(HttpStatusCode.InternalServerError, """{"error":"Unexpected request"}""");

            return _responses.Dequeue();
        }
    }

    private sealed record RecordedRequest(string Method, string Path, string Body);

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw new HttpRequestException("connection refused");
        }
    }
}
