using Nona.Application.Admin.Users.Commands;
using Nona.Application.Common;
using Nona.Application.Tests.Common;
using Nona.Domain.Entities;
using NSubstitute;

namespace Nona.Application.Tests.Users;

public class DeleteUserCommandTests
{
    private const long UserId = 42;

    [Test]
    public async Task Member_CannotDeleteUsers()
    {
        var fixture = new TestFixture();
        fixture.UserAuthorizationService.CanManageUsersAsync(Arg.Any<CancellationToken>()).Returns(false);

        var result = await CreateHandler(fixture).Handle(new DeleteUserCommand(UserId), CancellationToken.None);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Access denied");
        await Assert.That(result.ErrorCode).IsEqualTo(AuthorizationErrorCodes.AccessDenied);
        await fixture.UserRepository.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Admin_CannotDeleteSelf()
    {
        var fixture = new TestFixture();
        fixture.SetupAsSystemAdmin("admin@example.com");
        fixture.UserRepository.GetByIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(new User
        {
            Id = UserId,
            Email = "ADMIN@example.com",
            Name = "Admin",
            Role = UserRole.Admin
        });

        var result = await CreateHandler(fixture).Handle(new DeleteUserCommand(UserId), CancellationToken.None);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("You cannot delete your own user account");
        await fixture.UserRepository.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Admin_DeletesMemberAndMemberships()
    {
        var fixture = new TestFixture();
        fixture.SetupAsSystemAdmin("admin@example.com");
        fixture.UserRepository.GetByIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(new User
        {
            Id = UserId,
            Email = "member@example.com",
            Name = "Member",
            Role = UserRole.Member
        });

        var result = await CreateHandler(fixture).Handle(new DeleteUserCommand(UserId), CancellationToken.None);

        await Assert.That(result.Success).IsTrue();
        await fixture.ProjectMemberRepository.Received(1)
            .DeleteByUserAsync("member@example.com", Arg.Any<CancellationToken>());
        await fixture.UserRepository.Received(1)
            .DeleteAsync("member@example.com", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Admin_DeletesAnotherAdminWhenOneRemains()
    {
        var fixture = new TestFixture();
        fixture.SetupAsSystemAdmin("current@example.com");
        var current = new User { Email = "current@example.com", Name = "Current", Role = UserRole.Admin };
        var target = new User { Id = UserId, Email = "other@example.com", Name = "Other", Role = UserRole.Admin };
        fixture.UserRepository.GetByIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(target);
        fixture.UserRepository.ListAsync(Arg.Any<CancellationToken>()).Returns([current, target]);

        var result = await CreateHandler(fixture).Handle(new DeleteUserCommand(UserId), CancellationToken.None);

        await Assert.That(result.Success).IsTrue();
        await fixture.UserRepository.Received(1)
            .DeleteAsync("other@example.com", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Admin_CannotDeleteLastAdmin()
    {
        var fixture = new TestFixture();
        fixture.SetupAsSystemAdmin("current@example.com");
        var target = new User { Id = UserId, Email = "other@example.com", Name = "Other", Role = UserRole.Admin };
        fixture.UserRepository.GetByIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(target);
        fixture.UserRepository.ListAsync(Arg.Any<CancellationToken>()).Returns([target]);

        var result = await CreateHandler(fixture).Handle(new DeleteUserCommand(UserId), CancellationToken.None);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("At least one admin is required");
        await fixture.UserRepository.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private static DeleteUserCommandHandler CreateHandler(TestFixture fixture) => new(
        fixture.UserRepository,
        fixture.ProjectMemberRepository,
        fixture.UserAuthorizationService);
}
