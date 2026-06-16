using Nona.Application.Admin.Users.Commands;
using Nona.Application.Tests.Common;
using Nona.Domain.Entities;
using Nona.Domain.Enums;
using NSubstitute;

namespace Nona.Application.Tests.Users;

public class UpdateUserCommandTests
{
    private const long UserId = 42;
    private const string Email = "viewer@example.com";

    [Test]
    public async Task UpdateUser_AllowsViewerToUpdateOwnName()
    {
        var fixture = new TestFixture();
        fixture.CurrentUserService.Username.Returns(Email);
        fixture.CurrentUserService.Role.Returns(UserRole.Viewer);
        fixture.UserAuthorizationService.GetCurrentUserAsync(Arg.Any<CancellationToken>())
            .Returns(new User { Id = UserId, Email = Email, Name = "Old Name", Role = UserRole.Viewer });
        fixture.UserAuthorizationService.CanManageUsersAsync(Arg.Any<CancellationToken>()).Returns(false);
        fixture.DateTime.NowUtc.Returns(new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc));
        fixture.UserRepository.GetByIdAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new User
            {
                Id = UserId,
                Email = Email,
                Name = "Old Name",
                Role = UserRole.Viewer,
                Scope = KeyScope.All
            });
        fixture.ProjectMemberRepository.ListByUserAsync(Email, Arg.Any<CancellationToken>())
            .Returns([]);

        var handler = new UpdateUserCommandHandler(
            fixture.UserRepository,
            fixture.ProjectMemberRepository,
            fixture.DateTime,
            fixture.UserAuthorizationService);

        var result = await handler.Handle(new UpdateUserCommand(UserId, "New Name", null, null), CancellationToken.None);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.User!.Name).IsEqualTo("New Name");
        await fixture.UserRepository.Received(1).UpdateAsync(
            Arg.Is<User>(user => user.Email == Email && user.Name == "New Name"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task UpdateUser_RejectsViewerChangingOwnRoleOrScope()
    {
        var fixture = new TestFixture();
        fixture.CurrentUserService.Username.Returns(Email);
        fixture.CurrentUserService.Role.Returns(UserRole.Viewer);
        fixture.UserAuthorizationService.GetCurrentUserAsync(Arg.Any<CancellationToken>())
            .Returns(new User { Id = UserId, Email = Email, Name = "Viewer", Role = UserRole.Viewer });
        fixture.UserAuthorizationService.CanManageUsersAsync(Arg.Any<CancellationToken>()).Returns(false);
        fixture.UserRepository.GetByIdAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new User
            {
                Id = UserId,
                Email = Email,
                Name = "Viewer",
                Role = UserRole.Viewer,
                Scope = KeyScope.All
            });

        var handler = new UpdateUserCommandHandler(
            fixture.UserRepository,
            fixture.ProjectMemberRepository,
            fixture.DateTime,
            fixture.UserAuthorizationService);

        var result = await handler.Handle(new UpdateUserCommand(UserId, "Viewer", "editor", null), CancellationToken.None);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Access denied");
        await fixture.UserRepository.DidNotReceive().UpdateAsync(
            Arg.Any<User>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task UpdateUser_RejectsViewerUpdatingAnotherUser()
    {
        var fixture = new TestFixture();
        fixture.CurrentUserService.Username.Returns(Email);
        fixture.CurrentUserService.Role.Returns(UserRole.Viewer);
        fixture.UserAuthorizationService.GetCurrentUserAsync(Arg.Any<CancellationToken>())
            .Returns(new User { Email = Email, Name = "Viewer", Role = UserRole.Viewer });
        fixture.UserAuthorizationService.CanManageUsersAsync(Arg.Any<CancellationToken>()).Returns(false);
        fixture.UserRepository.GetByIdAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new User
            {
                Id = UserId,
                Email = "other@example.com",
                Name = "Other",
                Role = UserRole.Viewer,
                Scope = KeyScope.All
            });

        var handler = new UpdateUserCommandHandler(
            fixture.UserRepository,
            fixture.ProjectMemberRepository,
            fixture.DateTime,
            fixture.UserAuthorizationService);

        var result = await handler.Handle(new UpdateUserCommand(UserId, "Changed", null, null), CancellationToken.None);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Access denied");
        await fixture.UserRepository.DidNotReceive().UpdateAsync(
            Arg.Any<User>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task UpdateUser_UsesPersistedRoleAfterSelfDemotion()
    {
        var fixture = new TestFixture();
        fixture.CurrentUserService.Username.Returns(Email);
        fixture.CurrentUserService.Role.Returns(UserRole.Editor);
        fixture.UserAuthorizationService.GetCurrentUserAsync(Arg.Any<CancellationToken>())
            .Returns(new User { Id = 7, Email = Email, Name = "Former Editor", Role = UserRole.Viewer });
        fixture.UserAuthorizationService.CanManageUsersAsync(Arg.Any<CancellationToken>()).Returns(false);
        fixture.UserRepository.GetByIdAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new User
            {
                Id = UserId,
                Email = "other@example.com",
                Name = "Other",
                Role = UserRole.Viewer,
                Scope = KeyScope.All
            });

        var handler = new UpdateUserCommandHandler(
            fixture.UserRepository,
            fixture.ProjectMemberRepository,
            fixture.DateTime,
            fixture.UserAuthorizationService);

        var result = await handler.Handle(new UpdateUserCommand(UserId, "Changed", null, null), CancellationToken.None);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Access denied");
        await fixture.UserRepository.DidNotReceive().UpdateAsync(
            Arg.Any<User>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task UpdateUser_RejectsEditorUpdatingSystemAdmin()
    {
        var fixture = new TestFixture();
        fixture.UserAuthorizationService.GetCurrentUserAsync(Arg.Any<CancellationToken>())
            .Returns(new User { Id = 7, Email = "editor@example.com", Name = "Editor", Role = UserRole.Editor });
        fixture.UserRepository.GetByIdAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new User
            {
                Id = UserId,
                Email = "admin@example.com",
                Name = "Admin",
                IsAdmin = true,
                Role = UserRole.Viewer,
                Scope = KeyScope.All
            });

        var handler = new UpdateUserCommandHandler(
            fixture.UserRepository,
            fixture.ProjectMemberRepository,
            fixture.DateTime,
            fixture.UserAuthorizationService);

        var result = await handler.Handle(new UpdateUserCommand(UserId, "Changed", null, null), CancellationToken.None);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Access denied");
        await fixture.UserRepository.DidNotReceive().UpdateAsync(
            Arg.Any<User>(),
            Arg.Any<CancellationToken>());
    }
}
