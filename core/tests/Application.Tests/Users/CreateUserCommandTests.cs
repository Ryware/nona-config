using Nona.Application.Admin.Users.Commands;
using Nona.Application.Tests.Common;
using Nona.Domain.Entities;
using NSubstitute;

namespace Nona.Application.Tests.Users;

public class CreateUserCommandTests
{
    [Test]
    public async Task CreateUser_RejectsMember()
    {
        var fixture = new TestFixture();
        fixture.UserAuthorizationService.CanManageUsersAsync(Arg.Any<CancellationToken>()).Returns(false);

        var handler = new CreateUserCommandHandler(
            fixture.UserRepository,
            fixture.DateTime,
            fixture.UserAuthorizationService);

        var result = await handler.Handle(
            new CreateUserCommand("New User", "new@example.com", "member", "all"),
            CancellationToken.None);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Access denied");
        await fixture.UserRepository.DidNotReceive().AddAsync(
            Arg.Any<User>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task CreateUser_AdminCanInviteAnotherAdmin()
    {
        var fixture = new TestFixture();
        fixture.UserAuthorizationService.CanManageUsersAsync(Arg.Any<CancellationToken>()).Returns(true);
        fixture.DateTime.NowUtc.Returns(new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc));

        var handler = new CreateUserCommandHandler(
            fixture.UserRepository,
            fixture.DateTime,
            fixture.UserAuthorizationService);

        var result = await handler.Handle(
            new CreateUserCommand("New Admin", "new@example.com", "admin", "all"),
            CancellationToken.None);

        await Assert.That(result.Success).IsTrue();
        await fixture.UserRepository.Received(1).AddAsync(
            Arg.Is<User>(user =>
                user.Email == "new@example.com"
                && user.Name == "New Admin"
                && user.Role == UserRole.Admin),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task CreateUser_DefaultsToMemberWithoutProjects()
    {
        var fixture = new TestFixture();
        fixture.UserAuthorizationService.CanManageUsersAsync(Arg.Any<CancellationToken>()).Returns(true);

        var handler = new CreateUserCommandHandler(
            fixture.UserRepository,
            fixture.DateTime,
            fixture.UserAuthorizationService);

        var result = await handler.Handle(
            new CreateUserCommand("New Member", "member@example.com", null, null),
            CancellationToken.None);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Response!.User.Role).IsEqualTo("member");
        await Assert.That(result.Response.User.Projects).IsEmpty();
    }
}
