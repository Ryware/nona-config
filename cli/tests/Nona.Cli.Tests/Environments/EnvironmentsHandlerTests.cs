using System.Net;
using System.Text;
using Nona.Cli.Environments.Commands;
using Nona.Cli.Environments.Queries;
using Nona.Cli.Entries.Commands;
using Nona.Cli.Generated.Models;

#pragma warning disable TUnit0055

namespace Nona.Cli.Tests.Environments;

[NotInParallel]
public sealed class EnvironmentsHandlerTests
{
    private static readonly NonaCliConnectionOptions TestConnection = new("http://nona.test", "test-token");

    private const string EnvironmentJson = """
        {"name":"development","project":"my-project","activeReleaseVersion":null,"createdAt":"2026-07-20T00:00:00Z","updatedAt":"2026-07-20T00:00:00Z"}
        """;

    [Test]
    public async Task ListEnvironmentsQueryHandler_ListsEnvironments()
    {
        var factory = CreateHttpFactory((request, _) =>
        {
            AssertRequest(request, HttpMethod.Get, "/admin/projects/my-project/environments");
            return JsonResponse(HttpStatusCode.OK, $"[{EnvironmentJson}]");
        });

        var (result, output) = await CaptureOutputAsync(() =>
            new ListEnvironmentsQueryHandler(factory).HandleAsync(
                new ListEnvironmentsQuery(TestConnection, "my-project"),
                CancellationToken.None));

        await Assert.That(result).IsEqualTo(0);
        await Assert.That(output).Contains("Environments — my-project");
        await Assert.That(output).Contains("development");
        await Assert.That(output).Contains("Active release: (none)");
    }

    [Test]
    public async Task ListEnvironmentsQueryHandler_ReturnsZero_WhenEmpty()
    {
        var (result, output) = await CaptureOutputAsync(() =>
            new ListEnvironmentsQueryHandler(CreateHttpFactory((_, _) => JsonResponse(HttpStatusCode.OK, "[]")))
                .HandleAsync(new ListEnvironmentsQuery(TestConnection, "my-project"), CancellationToken.None));

        await Assert.That(result).IsEqualTo(0);
        await Assert.That(output).Contains("No environments found for project: my-project");
    }

    [Test]
    public async Task CreateEnvironmentCommandHandler_CreatesEnvironment()
    {
        string? requestBody = null;
        var factory = CreateHttpFactory(async (request, _) =>
        {
            AssertRequest(request, HttpMethod.Post, "/admin/projects/my-project/environments");
            requestBody = await request.Content!.ReadAsStringAsync();
            return JsonResponse(HttpStatusCode.Created, EnvironmentJson);
        });

        var (result, output) = await CaptureOutputAsync(() =>
            new CreateEnvironmentCommandHandler(factory).HandleAsync(
                new CreateEnvironmentCommand(TestConnection, "my-project", "development"),
                CancellationToken.None));

        await Assert.That(result).IsEqualTo(0);
        await Assert.That(output).Contains("Created environment: development");
        await Assert.That(requestBody).Contains("\"name\":\"development\"");
    }

    [Test]
    public async Task CreateEnvironmentCommandHandler_ReturnsSuccess_WhenEnvironmentAlreadyExists()
    {
        var factory = CreateHttpFactory((request, requestNumber) => requestNumber switch
        {
            1 => ProblemResponse(HttpStatusCode.Conflict,
                """{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.10","title":"Conflict","status":409,"detail":"Environment already exists","instance":"/admin/projects/my-project/environments","errorCode":"ENVIRONMENT_EXISTS"}"""),
            2 => JsonResponse(HttpStatusCode.OK, $"[{EnvironmentJson}]"),
            _ => throw new InvalidOperationException($"Unexpected request: {request.Method} {request.RequestUri}")
        });

        var (result, output) = await CaptureOutputAsync(() =>
            new CreateEnvironmentCommandHandler(factory).HandleAsync(
                new CreateEnvironmentCommand(TestConnection, "my-project", "Development"),
                CancellationToken.None));

        await Assert.That(result).IsEqualTo(0);
        await Assert.That(output).Contains("Environment already exists: development");
    }

    [Test]
    public async Task CreateEnvironmentCommandHandler_DoesNotHideUnconfirmedConflict()
    {
        var factory = CreateHttpFactory((_, requestNumber) => requestNumber switch
        {
            1 => ProblemResponse(HttpStatusCode.Conflict,
                """{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.10","title":"Conflict","status":409,"detail":"Environment already exists","instance":"/admin/projects/my-project/environments","errorCode":"ENVIRONMENT_EXISTS"}"""),
            2 => JsonResponse(HttpStatusCode.OK, "[]"),
            _ => throw new InvalidOperationException("Unexpected request")
        });

        ApiProblemDetails? error = null;
        try
        {
            await new CreateEnvironmentCommandHandler(factory).HandleAsync(
                new CreateEnvironmentCommand(TestConnection, "my-project", "development"),
                CancellationToken.None);
        }
        catch (ApiProblemDetails ex)
        {
            error = ex;
        }

        await Assert.That(error).IsNotNull();
        await Assert.That(error!.ResponseStatusCode).IsEqualTo((int)HttpStatusCode.Conflict);
    }

    [Test]
    public async Task DeleteEnvironmentCommandHandler_DeletesEnvironment()
    {
        var factory = CreateHttpFactory((request, _) =>
        {
            AssertRequest(request, HttpMethod.Delete, "/admin/projects/my-project/environments/development");
            return JsonResponse(HttpStatusCode.NoContent, string.Empty);
        });

        var (result, output) = await CaptureOutputAsync(() =>
            new DeleteEnvironmentCommandHandler(factory).HandleAsync(
                new DeleteEnvironmentCommand(TestConnection, "my-project", "development"),
                CancellationToken.None));

        await Assert.That(result).IsEqualTo(0);
        await Assert.That(output).Contains("Deleted environment: development");
    }

    [Test]
    public async Task CreatedEnvironment_CanReceiveConfigEntry()
    {
        var environments = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var factory = CreateHttpFactory((request, _) =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Post && path.EndsWith("/environments", StringComparison.Ordinal))
            {
                environments.Add("development");
                return JsonResponse(HttpStatusCode.Created, EnvironmentJson);
            }

            if (request.Method == HttpMethod.Put &&
                path.Contains("/environments/development/config-entries/", StringComparison.Ordinal) &&
                environments.Contains("development"))
            {
                return JsonResponse(HttpStatusCode.OK,
                    """{"project":"my-project","environment":"development","key":"Features:DarkMode","value":"false","contentType":"boolean","scope":"client","activeVersion":1,"createdAt":"2026-07-20T00:00:00Z","updatedAt":"2026-07-20T00:00:00Z"}"""
                );
            }

            return ProblemResponse(HttpStatusCode.NotFound,
                """{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.5","title":"Not Found","status":404,"detail":"Environment not found","instance":"/admin/projects/my-project/environments/development/config-entries/Features%3ADarkMode","errorCode":"ENVIRONMENT_NOT_FOUND"}"""
            );
        });

        var createResult = await new CreateEnvironmentCommandHandler(factory).HandleAsync(
            new CreateEnvironmentCommand(TestConnection, "my-project", "development"),
            CancellationToken.None);
        var setResult = await new SetEntryCommandHandler(factory).HandleAsync(
            new SetEntryCommand(
                TestConnection,
                "my-project",
                "development",
                "Features:DarkMode",
                "false",
                "client",
                "boolean"),
            CancellationToken.None);

        await Assert.That(createResult).IsEqualTo(0);
        await Assert.That(setResult).IsEqualTo(0);
    }

    [Test]
    public async Task SetEntryAgainstMissingEnvironment_MapsToCleanNotFoundError()
    {
        var factory = CreateHttpFactory((_, _) => ProblemResponse(
            HttpStatusCode.NotFound,
            """{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.5","title":"Not Found","status":404,"detail":"Environment not found","instance":"/admin/projects/my-project/environments/missing/config-entries/Features%3ADarkMode","errorCode":"ENVIRONMENT_NOT_FOUND"}"""));

        ApiProblemDetails? exception = null;
        try
        {
            await new SetEntryCommandHandler(factory).HandleAsync(
                new SetEntryCommand(
                    TestConnection,
                    "my-project",
                    "missing",
                    "Features:DarkMode",
                    "false",
                    "client",
                    "boolean"),
                CancellationToken.None);
        }
        catch (ApiProblemDetails ex)
        {
            exception = ex;
        }

        await Assert.That(exception).IsNotNull();
        var error = CliExceptionHandler.Describe(exception!);
        await Assert.That(error.ExitCode).IsEqualTo(CliExitCodes.NotFound);
        await Assert.That(error.Message).IsEqualTo(
            "Error: Environment not found (404, ENVIRONMENT_NOT_FOUND)");
        await Assert.That(error.Message).DoesNotContain(nameof(ApiProblemDetails));
    }

    private static Func<HttpClient> CreateHttpFactory(
        Func<HttpRequestMessage, int, HttpResponseMessage> responder)
        => CreateHttpFactory((request, requestNumber) =>
            Task.FromResult(responder(request, requestNumber)));

    private static Func<HttpClient> CreateHttpFactory(
        Func<HttpRequestMessage, int, Task<HttpResponseMessage>> responder)
    {
        var requestNumber = 0;
        return () => new HttpClient(new StubHttpMessageHandler(request =>
            responder(request, Interlocked.Increment(ref requestNumber))));
    }

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

    private static void AssertRequest(HttpRequestMessage request, HttpMethod method, string path)
    {
        if (request.Method != method || request.RequestUri?.AbsolutePath != path)
            throw new InvalidOperationException($"Unexpected request: {request.Method} {request.RequestUri}");
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

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => responder(request);
    }
}
