using System.Net;
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
        await Assert.That(error!.ResponseStatusCode).IsEqualTo((int)statusCode);
        await Assert.That(error.Detail).IsEqualTo(serverMessage);
        await Assert.That(error.ErrorCode).IsEqualTo("TEST_ERROR");
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
}
