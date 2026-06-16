using Nona.Application.Admin.Users.Commands;
using Nona.Application.Tests.Common;
using Nona.Domain.Entities;
using NSubstitute;

namespace Nona.Application.Tests.Users;

public class DeleteUserCommandTests
{
    private const long UserId = 42;
    private const string Email = "user@example.com";

    [Test]
    public async Task DeleteUser_RejectsDeletingCurrentUser()
    {
        var fixture = new TestFixture();
        fixture.CurrentUserService.Username.Returns("USER@example.com");
        fixture.UserRepository.GetByIdAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new User
            {
                Id = UserId,
                Email = Email,
                Name = "User"
            });

        var handler = new DeleteUserCommandHandler(
            fixture.UserRepository,
            fixture.ProjectMemberRepository,
            fixture.CurrentUserService);

        var result = await handler.Handle(new DeleteUserCommand(UserId), CancellationToken.None);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("You cannot delete your own user account");
        await fixture.ProjectMemberRepository.DidNotReceive()
            .DeleteByUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await fixture.UserRepository.DidNotReceive()
            .DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DeleteUser_DeletesDifferentUser()
    {
        var fixture = new TestFixture();
        fixture.CurrentUserService.Username.Returns("admin@example.com");
        fixture.UserRepository.GetByIdAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new User
            {
                Id = UserId,
                Email = Email,
                Name = "User"
            });

        var handler = new DeleteUserCommandHandler(
            fixture.UserRepository,
            fixture.ProjectMemberRepository,
            fixture.CurrentUserService);

        var result = await handler.Handle(new DeleteUserCommand(UserId), CancellationToken.None);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Error).IsNull();
        await fixture.ProjectMemberRepository.Received(1)
            .DeleteByUserAsync(Email, Arg.Any<CancellationToken>());
        await fixture.UserRepository.Received(1)
            .DeleteAsync(Email, Arg.Any<CancellationToken>());
    }
}
