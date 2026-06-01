using System.Net;
using Nona.Cli.Keys.Commands;
using Nona.Cli.Keys.Queries;
using static Nona.Cli.Tests.Fixtures;
using static Nona.Cli.Tests.TestHelpers;

namespace Nona.Cli.Tests.Keys;

public sealed class KeysHandlerTests
{
    private static readonly NonaCliConnectionOptions TestConnection = new("http://nona.test", "test-token");

    [Test]
    public async Task ShowKeysQueryHandler_ReturnsZero_WhenProjectFound()
    {
        var result = await new ShowKeysQueryHandler(MockHttp(HttpStatusCode.OK, ApiKeyArrayJson))
            .HandleAsync(new ShowKeysQuery(TestConnection, "my-project"), CancellationToken.None);
        await Assert.That(result).IsEqualTo(0);
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
        var result = await new CreateApiKeyCommandHandler(MockHttp(HttpStatusCode.Created, ApiKeyJson))
            .HandleAsync(new CreateApiKeyCommand(TestConnection, "my-project", "Web Client", "production", "client"), CancellationToken.None);
        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    public async Task DeleteApiKeyCommandHandler_ReturnsZero_OnSuccess()
    {
        var result = await new DeleteApiKeyCommandHandler(MockHttp(HttpStatusCode.NoContent, string.Empty))
            .HandleAsync(new DeleteApiKeyCommand(TestConnection, "my-project", 7), CancellationToken.None);
        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    public async Task RerollKeysCommandHandler_ReturnsZero_OnSuccess()
    {
        var result = await new RerollKeysCommandHandler(MockHttp(HttpStatusCode.OK, ProjectJson))
            .HandleAsync(new RerollKeysCommand(TestConnection, "my-project", "both"), CancellationToken.None);
        await Assert.That(result).IsEqualTo(0);
    }
}
