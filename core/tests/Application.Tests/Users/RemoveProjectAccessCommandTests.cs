using Nona.Application.Admin.Users.Commands;
using Nona.Application.Common;
using Nona.Application.Tests.Common;
using Nona.Domain.Entities;
using NSubstitute;

namespace Nona.Application.Tests.Users;

public class RemoveProjectAccessCommandTests
{
    [Test]
    public async Task Member_IsDeniedWithStableErrorCode()
    {
        var fixture = new TestFixture();
        fixture.UserAuthorizationService.CanManageUsersAsync(Arg.Any<CancellationToken>()).Returns(false);
        var handler = new RemoveProjectAccessCommandHandler(
            fixture.UserRepository,
            fixture.ProjectRepository,
            fixture.ProjectMemberRepository,
            fixture.UserAuthorizationService);

        var result = await handler.Handle(
            new RemoveProjectAccessCommand(42, "project"),
            CancellationToken.None);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.ErrorCode).IsEqualTo(AuthorizationErrorCodes.AccessDenied);
        await fixture.ProjectMemberRepository.DidNotReceive().DeleteAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }
}
