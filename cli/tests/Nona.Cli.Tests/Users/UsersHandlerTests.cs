using System.Net;
using Nona.Cli.Users.Commands;
using static Nona.Cli.Tests.Fixtures;
using static Nona.Cli.Tests.TestHelpers;

namespace Nona.Cli.Tests.Users;

public sealed class UsersHandlerTests
{
    private static readonly NonaCliConnectionOptions TestConnection = new("http://nona.test", "test-token");

    [Test]
    public async Task CreateUserCommandHandler_ReturnsZero_OnSuccess()
    {
        var result = await new CreateUserCommandHandler(MockHttp(HttpStatusCode.Created, CreateUserResponseJson))
            .HandleAsync(new CreateUserCommand(TestConnection, "Test User", "user@example.com", "editor", "all"),
                CancellationToken.None);
        await Assert.That(result).IsEqualTo(0);
    }
}
