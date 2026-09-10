using System.Net;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using System.CommandLine.Parsing;
using Nona.Cli.Entries;
using Nona.Cli.Entries.Commands;
using Nona.Cli.Entries.Queries;
using Nona.Cli.Generated.Models;
using static Nona.Cli.Tests.Fixtures;
using static Nona.Cli.Tests.TestHelpers;

namespace Nona.Cli.Tests.Entries;

public sealed class EntriesHandlerTests
{
    private static readonly NonaCliConnectionOptions TestConnection = new("http://nona.test", "test-token");
    private static readonly NonaCliConnectionOptions ApiKeyConnection = new("http://nona.test", new string('A', 64));

    [Test]
    public async Task ListEntriesQueryHandler_ReturnsZero_WithEntries()
    {
        var result = await new ListEntriesQueryHandler(MockHttp(HttpStatusCode.OK, ConfigEntryArrayJson))
            .HandleAsync(new ListEntriesQuery(TestConnection, "my-project", "production"), CancellationToken.None);
        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    public async Task ListEntriesQueryHandler_ReturnsZero_WhenEmpty()
    {
        var result = await new ListEntriesQueryHandler(MockHttp(HttpStatusCode.OK, "[]"))
            .HandleAsync(new ListEntriesQuery(TestConnection, "my-project", "production"), CancellationToken.None);
        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    public async Task ListEntriesQueryHandler_ForwardsEncodedPrefix()
    {
        Uri? requestedUri = null;
        var result = await new ListEntriesQueryHandler(() => new HttpClient(
                new RecordingHandler(request =>
                {
                    requestedUri = request.RequestUri;
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json")
                    };
                })))
            .HandleAsync(
                new ListEntriesQuery(TestConnection, "my-project", "production", "GroupA:"),
                CancellationToken.None);

        await Assert.That(result).IsEqualTo(0);
        await Assert.That(requestedUri).IsNotNull();
        await Assert.That(requestedUri!.Query).IsEqualTo("?prefix=GroupA%3A");
    }

    [Test]
    public async Task ListEntriesQueryHandler_InvalidPrefixPrintsValidationErrorAndReturnsTwo()
    {
        const string validationMessage =
            "Prefix may contain only ASCII letters, digits, colons, dots, underscores, and dashes.";
        var handler = new ListEntriesQueryHandler(MockHttp(
            HttpStatusCode.BadRequest,
            $$"""{"title":"Bad Request","status":400,"detail":"{{validationMessage}}"}"""));
        ApiProblemDetails? exception = null;
        try
        {
            await handler.HandleAsync(
                new ListEntriesQuery(TestConnection, "my-project", "production", "%"),
                CancellationToken.None);
        }
        catch (ApiProblemDetails caught)
        {
            exception = caught;
        }

        await Assert.That(exception).IsNotNull();
        var root = new RootCommand();
        var verboseOption = new Option<bool>("--verbose");
        root.AddGlobalOption(verboseOption);
        root.SetHandler((InvocationContext _) => throw exception!);
        var console = new TestConsole();

        var exitCode = await Program.CreateParser(root, verboseOption).InvokeAsync([], console);

        await Assert.That(exitCode).IsEqualTo(CliExitCodes.ValidationError);
        await Assert.That(console.Out.ToString()).IsEmpty();
        await Assert.That(console.Error.ToString()).IsEqualTo(
            $"Error: {validationMessage} (400){Environment.NewLine}");
    }

    [Test]
    public async Task GetEntryQueryHandler_ReturnsZero_WhenFound()
    {
        var result = await new GetEntryQueryHandler(MockHttp(HttpStatusCode.OK, ConfigEntryJson))
            .HandleAsync(new GetEntryQuery(TestConnection, "my-project", "production", "my.key"), CancellationToken.None);
        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    public async Task GetEntryQueryHandler_ReturnsZero_WhenRawValueFound()
    {
        var result = await new GetEntryQueryHandler(MockHttp(
                HttpStatusCode.OK,
                """{"enabled":true}""",
                new Dictionary<string, string> { [ConfigEntryValueRenderer.LogicalContentTypeHeader] = "json" }))
            .HandleAsync(new GetEntryQuery(ApiKeyConnection, "my-project", "production", "my.key"), CancellationToken.None);
        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    public async Task GetEntryQueryHandler_ApiKeyDefaultsToWorkingParameterRoute()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new GetEntryQueryHandler(() => new HttpClient(
            new RecordingHandler(request =>
            {
                capturedRequest = request;
                return JsonResponse(HttpStatusCode.OK, "enabled");
            })));

        var result = await handler.HandleAsync(
            new GetEntryQuery(
                new NonaCliConnectionOptions("https://nona.test/proxy/tenant", ApiKeyConnection.BearerToken),
                "my-project",
                "pre production",
                "Features:Checkout"),
            CancellationToken.None);

        await Assert.That(result).IsEqualTo(CliExitCodes.Success);
        await Assert.That(capturedRequest).IsNotNull();
        await Assert.That(capturedRequest!.RequestUri!.AbsoluteUri).IsEqualTo(
            "https://nona.test/proxy/tenant/api/environments/pre%20production/parameters/Features%3ACheckout");
        await Assert.That(capturedRequest.Headers.GetValues("X-Api-Key").Single())
            .IsEqualTo(ApiKeyConnection.BearerToken);
    }

    [Test]
    public async Task GetEntryQueryHandler_ApiKeyUsesActiveReleaseRoute()
    {
        Uri? requestedUri = null;
        var handler = RawHandler(request => requestedUri = request.RequestUri);

        var result = await handler.HandleAsync(
            new GetEntryQuery(
                ApiKeyConnection,
                "my-project",
                "production",
                "my.key",
                UseReleases: true),
            CancellationToken.None);

        await Assert.That(result).IsEqualTo(CliExitCodes.Success);
        await Assert.That(requestedUri!.AbsoluteUri).IsEqualTo(
            "http://nona.test/api/environments/production/releases/active/parameters/my.key");
    }

    [Test]
    [Arguments("1.2.3")]
    [Arguments("1.2.x")]
    public async Task GetEntryQueryHandler_ApiKeyUsesSelectedReleaseRoute(string selector)
    {
        Uri? requestedUri = null;
        var handler = RawHandler(request => requestedUri = request.RequestUri);

        var result = await handler.HandleAsync(
            new GetEntryQuery(
                ApiKeyConnection,
                "my-project",
                "production",
                "my.key",
                UseReleases: true,
                ReleaseVersion: $"  {selector}  "),
            CancellationToken.None);

        await Assert.That(result).IsEqualTo(CliExitCodes.Success);
        await Assert.That(requestedUri!.AbsoluteUri).IsEqualTo(
            $"http://nona.test/api/environments/production/releases/{selector}/parameters/my.key");
    }

    [Test]
    public async Task GetEntryQueryHandler_IgnoresReleaseVersionOutsideReleaseMode()
    {
        Uri? requestedUri = null;
        var handler = RawHandler(request => requestedUri = request.RequestUri);

        var result = await handler.HandleAsync(
            new GetEntryQuery(
                ApiKeyConnection,
                "my-project",
                "production",
                "my.key",
                ReleaseVersion: ".."),
            CancellationToken.None);

        await Assert.That(result).IsEqualTo(CliExitCodes.Success);
        await Assert.That(requestedUri!.AbsoluteUri).IsEqualTo(
            "http://nona.test/api/environments/production/parameters/my.key");
    }

    [Test]
    [Arguments(".")]
    [Arguments(" .. ")]
    public async Task GetEntryQueryHandler_RejectsDotSegmentReleaseSelectorWithoutRequest(string selector)
    {
        var requestCount = 0;
        var handler = RawHandler(_ => requestCount++);

        var result = await handler.HandleAsync(
            new GetEntryQuery(
                ApiKeyConnection,
                "my-project",
                "production",
                "my.key",
                UseReleases: true,
                ReleaseVersion: selector),
            CancellationToken.None);

        await Assert.That(result).IsEqualTo(CliExitCodes.ValidationError);
        await Assert.That(requestCount).IsEqualTo(0);
    }

    [Test]
    public async Task GetEntryQueryHandler_RejectsReleaseModeForAdminTokenWithoutRequest()
    {
        var requestCount = 0;
        var handler = RawHandler(_ => requestCount++);

        var result = await handler.HandleAsync(
            new GetEntryQuery(
                TestConnection,
                "my-project",
                "production",
                "my.key",
                UseReleases: true),
            CancellationToken.None);

        await Assert.That(result).IsEqualTo(CliExitCodes.ValidationError);
        await Assert.That(requestCount).IsEqualTo(0);
    }

    [Test]
    [Arguments(HttpStatusCode.BadRequest, "invalid_release_version", CliExitCodes.ValidationError)]
    [Arguments(HttpStatusCode.Unauthorized, "invalid_api_key", CliExitCodes.AuthenticationError)]
    [Arguments(HttpStatusCode.NotFound, "config_entry_not_found", CliExitCodes.NotFound)]
    [Arguments(HttpStatusCode.Conflict, "active_release_not_configured", CliExitCodes.Conflict)]
    [Arguments(HttpStatusCode.InternalServerError, "server_error", CliExitCodes.ServerError)]
    public async Task GetEntryQueryHandler_PreservesRuntimeProblemDetails(
        HttpStatusCode statusCode,
        string errorCode,
        int expectedExitCode)
    {
        var handler = new GetEntryQueryHandler(() => new HttpClient(
            new RecordingHandler(_ => JsonResponse(
                statusCode,
                $$"""{"title":"Request failed","status":{{(int)statusCode}},"detail":"runtime detail","errorCode":"{{errorCode}}"}"""))));

        ApiProblemDetails? problem = null;
        try
        {
            await handler.HandleAsync(
                new GetEntryQuery(ApiKeyConnection, "my-project", "production", "my.key"),
                CancellationToken.None);
        }
        catch (ApiProblemDetails ex)
        {
            problem = ex;
        }

        await Assert.That(problem).IsNotNull();
        await Assert.That(problem!.ResponseStatusCode).IsEqualTo((int)statusCode);
        await Assert.That(problem.ErrorCode).IsEqualTo(errorCode);
        await Assert.That(problem.Detail).IsEqualTo("runtime detail");

        var cliError = CliExceptionHandler.Describe(problem);
        await Assert.That(cliError.ExitCode).IsEqualTo(expectedExitCode);
        await Assert.That(cliError.Message).Contains($"({(int)statusCode}, {errorCode})");
    }

    [Test]
    public async Task GetEntryQueryHandler_AdminTokenStillUsesWorkingAdminRoute()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new GetEntryQueryHandler(() => new HttpClient(
            new RecordingHandler(request =>
            {
                capturedRequest = request;
                return JsonResponse(HttpStatusCode.OK, ConfigEntryJson);
            })));

        var result = await handler.HandleAsync(
            new GetEntryQuery(
                TestConnection,
                "my-project",
                "production",
                "my.key",
                ReleaseVersion: "1.2.x"),
            CancellationToken.None);

        await Assert.That(result).IsEqualTo(CliExitCodes.Success);
        await Assert.That(capturedRequest!.RequestUri!.AbsolutePath).IsEqualTo(
            "/admin/projects/my-project/environments/production/config-entries/my.key");
        await Assert.That(capturedRequest.Headers.Authorization!.Scheme).IsEqualTo("Bearer");
        await Assert.That(capturedRequest.Headers.Authorization.Parameter).IsEqualTo(TestConnection.BearerToken);
    }

    [Test]
    public async Task GetEntryQueryHandler_ReturnsOne_WhenNotFound()
    {
        var result = await new GetEntryQueryHandler(MockHttp(HttpStatusCode.NotFound, string.Empty))
            .HandleAsync(new GetEntryQuery(TestConnection, "my-project", "production", "missing.key"), CancellationToken.None);
        await Assert.That(result).IsEqualTo(1);
    }

    [Test]
    public async Task SetEntryCommandHandler_ReturnsZero_OnSuccess()
    {
        var result = await new SetEntryCommandHandler(MockHttp(HttpStatusCode.OK, string.Empty))
            .HandleAsync(new SetEntryCommand(TestConnection, "my-project", "production", "my.key", "my-value", "all", null),
                CancellationToken.None);
        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    [Arguments(HttpStatusCode.BadRequest, "value is not a valid number")]
    [Arguments(HttpStatusCode.NotFound, "environment not found")]
    public async Task SetEntryCommandHandler_PreservesServerErrorBody(
        HttpStatusCode statusCode,
        string serverMessage)
    {
        var handler = new SetEntryCommandHandler(MockHttp(
            statusCode,
            $$"""{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.1","title":"Request failed","status":{{(int)statusCode}},"detail":"{{serverMessage}}","instance":"/admin/test","errorCode":"TEST_ERROR"}"""));

        ApiProblemDetails? error = null;
        try
        {
            await handler.HandleAsync(
                new SetEntryCommand(TestConnection, "my-project", "production", "my.key", "bad", "client", "number"),
                CancellationToken.None);
        }
        catch (ApiProblemDetails ex)
        {
            error = ex;
        }

        await Assert.That(error).IsNotNull();
        var capturedError = error!;
        await Assert.That(capturedError.ResponseStatusCode).IsEqualTo((int)statusCode);
        await Assert.That(capturedError.Detail).IsEqualTo(serverMessage);
        await Assert.That(capturedError.ErrorCode).IsEqualTo("TEST_ERROR");
    }

    [Test]
    public async Task HistoryEntriesQueryHandler_ReturnsZero_WithVersions()
    {
        var result = await new HistoryEntriesQueryHandler(MockHttp(HttpStatusCode.OK, ConfigEntryVersionArrayJson))
            .HandleAsync(new HistoryEntriesQuery(TestConnection, "my-project", "production", "my.key"),
                CancellationToken.None);
        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    public async Task RollbackEntryCommandHandler_ReturnsZero_OnSuccess()
    {
        var result = await new RollbackEntryCommandHandler(MockHttp(HttpStatusCode.OK, ConfigEntryJson))
            .HandleAsync(new RollbackEntryCommand(TestConnection, "my-project", "production", "my.key", 1),
                CancellationToken.None);
        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    public async Task DeleteEntryCommandHandler_ReturnsZero_OnSuccess()
    {
        var result = await new DeleteEntryCommandHandler(MockHttp(HttpStatusCode.NoContent, string.Empty))
            .HandleAsync(new DeleteEntryCommand(TestConnection, "my-project", "production", "my.key"),
                CancellationToken.None);
        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    public async Task ListEntryShareLinksQueryHandler_ReturnsZero_WithLinks()
    {
        var result = await new ListEntryShareLinksQueryHandler(MockHttp(HttpStatusCode.OK, ParameterShareLinkArrayJson))
            .HandleAsync(new ListEntryShareLinksQuery(TestConnection, "my-project", "production", "my.key"),
                CancellationToken.None);
        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    public async Task CreateEntryShareLinkCommandHandler_ReturnsZero_OnSuccess()
    {
        var result = await new CreateEntryShareLinkCommandHandler(MockHttp(HttpStatusCode.Created, CreatedParameterShareLinkJson))
            .HandleAsync(new CreateEntryShareLinkCommand(
                    TestConnection,
                    "my-project",
                    "production",
                    "my.key",
                    "1h",
                    false,
                    "https://admin.nona.test"),
                CancellationToken.None);
        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    public async Task RevokeEntryShareLinkCommandHandler_ReturnsZero_OnSuccess()
    {
        var result = await new RevokeEntryShareLinkCommandHandler(MockHttp(HttpStatusCode.NoContent, string.Empty))
            .HandleAsync(new RevokeEntryShareLinkCommand(TestConnection, "my-project", "production", "my.key", 11),
                CancellationToken.None);
        await Assert.That(result).IsEqualTo(0);
    }

    private sealed class RecordingHandler(
        Func<HttpRequestMessage, HttpResponseMessage> handle) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(handle(request));
    }

    private static GetEntryQueryHandler RawHandler(Action<HttpRequestMessage> capture)
        => new(() => new HttpClient(new RecordingHandler(request =>
        {
            capture(request);
            return JsonResponse(HttpStatusCode.OK, "enabled");
        })));

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string body)
        => new(status)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
        };
}
