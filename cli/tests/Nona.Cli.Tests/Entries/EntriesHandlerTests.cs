using System.Net;
using Nona.Cli.Entries;
using Nona.Cli.Entries.Commands;
using Nona.Cli.Entries.Queries;
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
    public async Task DeleteEntryCommandHandler_ReturnsZero_OnSuccess()
    {
        var result = await new DeleteEntryCommandHandler(MockHttp(HttpStatusCode.NoContent, string.Empty))
            .HandleAsync(new DeleteEntryCommand(TestConnection, "my-project", "production", "my.key"),
                CancellationToken.None);
        await Assert.That(result).IsEqualTo(0);
    }
}
