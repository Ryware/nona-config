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
        var result = await new ShowKeysQueryHandler(MockHttp(HttpStatusCode.OK, ProjectArrayJson))
            .HandleAsync(new ShowKeysQuery(TestConnection, "my-project"), CancellationToken.None);
        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    public async Task ShowKeysQueryHandler_ReturnsOne_WhenProjectNotFound()
    {
        var result = await new ShowKeysQueryHandler(MockHttp(HttpStatusCode.OK, "[]"))
            .HandleAsync(new ShowKeysQuery(TestConnection, "missing-project"), CancellationToken.None);
        await Assert.That(result).IsEqualTo(1);
    }

    [Test]
    public async Task RerollKeysCommandHandler_ReturnsZero_OnSuccess()
    {
        var result = await new RerollKeysCommandHandler(MockHttp(HttpStatusCode.OK, ProjectJson))
            .HandleAsync(new RerollKeysCommand(TestConnection, "my-project", "both"), CancellationToken.None);
        await Assert.That(result).IsEqualTo(0);
    }
}
