using Nona.Application.Admin.Users.Commands;
using Nona.Application.Tests.Common;
using Nona.Domain.Entities;
using Nona.Domain.Enums;
using NSubstitute;

namespace Nona.Application.Tests.Users;

public class UpdateUserCommandTests
{
    private const long UserId = 42;

    [Test]
    public async Task Member_CannotUpdateSelf()
    {
        var fixture = new TestFixture();
        fixture.UserAuthorizationService.CanManageUsersAsync(Arg.Any<CancellationToken>()).Returns(false);

        var result = await CreateHandler(fixture).Handle(
            new UpdateUserCommand(UserId, "New Name", null, null),
            CancellationToken.None);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Access denied");
        await fixture.UserRepository.DidNotReceive().UpdateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Admin_PromotesMemberAndPreservesMemberships()
    {
        var fixture = new TestFixture();
        fixture.SetupAsSystemAdmin("current@example.com");
        var member = CreateUser(UserRole.Member);
        var membership = new ProjectMember
        {
            Username = member.Email,
            ProjectId = "alpha",
            Role = ProjectRole.Editor
        };
        fixture.UserRepository.GetByIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(member);
        fixture.ProjectMemberRepository.ListByUserAsync(member.Email, Arg.Any<CancellationToken>())
            .Returns([membership]);

        var result = await CreateHandler(fixture).Handle(
            new UpdateUserCommand(UserId, member.Name, "admin", null),
            CancellationToken.None);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.User!.Role).IsEqualTo("admin");
        await Assert.That(result.User.Projects.Single().Role).IsEqualTo("editor");
        await fixture.ProjectMemberRepository.DidNotReceive()
            .DeleteByUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Admin_DemotesAnotherAdminAndPreservesMemberships()
    {
        var fixture = new TestFixture();
        fixture.SetupAsSystemAdmin("current@example.com");
        var current = new User { Email = "current@example.com", Name = "Current", Role = UserRole.Admin };
        var target = CreateUser(UserRole.Admin);
        var membership = new ProjectMember
        {
            Username = target.Email,
            ProjectId = "alpha",
            Role = ProjectRole.Viewer
        };
        fixture.UserRepository.GetByIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(target);
        fixture.UserRepository.ListAsync(Arg.Any<CancellationToken>()).Returns([current, target]);
        fixture.ProjectMemberRepository.ListByUserAsync(target.Email, Arg.Any<CancellationToken>())
            .Returns([membership]);

        var result = await CreateHandler(fixture).Handle(
            new UpdateUserCommand(UserId, target.Name, "member", null),
            CancellationToken.None);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.User!.Role).IsEqualTo("member");
        await Assert.That(result.User.Projects.Single().Role).IsEqualTo("viewer");
        await fixture.ProjectMemberRepository.DidNotReceive()
            .DeleteByUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Admin_CannotDemoteSelf()
    {
        var fixture = new TestFixture();
        fixture.SetupAsSystemAdmin("member@example.com");
        fixture.UserRepository.GetByIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(CreateUser(UserRole.Admin));

        var result = await CreateHandler(fixture).Handle(
            new UpdateUserCommand(UserId, "Member", "member", null),
            CancellationToken.None);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("You cannot demote your own admin account");
    }

    [Test]
    public async Task Admin_CannotDemoteLastAdmin()
    {
        var fixture = new TestFixture();
        fixture.SetupAsSystemAdmin("current@example.com");
        var target = CreateUser(UserRole.Admin);
        fixture.UserRepository.GetByIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(target);
        fixture.UserRepository.ListAsync(Arg.Any<CancellationToken>()).Returns([target]);

        var result = await CreateHandler(fixture).Handle(
            new UpdateUserCommand(UserId, target.Name, "member", null),
            CancellationToken.None);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("At least one admin is required");
    }

    [Test]
    public async Task Admin_CanKeepAdminRoleWhileUpdatingName()
    {
        var fixture = new TestFixture();
        fixture.SetupAsSystemAdmin("member@example.com");
        var admin = CreateUser(UserRole.Admin);
        fixture.UserRepository.GetByIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(admin);
        fixture.ProjectMemberRepository.ListByUserAsync(admin.Email, Arg.Any<CancellationToken>()).Returns([]);

        var result = await CreateHandler(fixture).Handle(
            new UpdateUserCommand(UserId, "Updated Admin", "admin", "all"),
            CancellationToken.None);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.User!.Name).IsEqualTo("Updated Admin");
        await Assert.That(result.User.Role).IsEqualTo("admin");
    }

    [Test]
    public async Task Admin_CanUpdateRoleWithoutSendingName()
    {
        var fixture = new TestFixture();
        fixture.SetupAsSystemAdmin("current@example.com");
        var member = CreateUser(UserRole.Member);
        fixture.UserRepository.GetByIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(member);
        fixture.ProjectMemberRepository.ListByUserAsync(member.Email, Arg.Any<CancellationToken>()).Returns([]);

        var result = await CreateHandler(fixture).Handle(
            new UpdateUserCommand(UserId, null, "admin", null),
            CancellationToken.None);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.User!.Name).IsEqualTo("Member");
        await fixture.UserRepository.Received(1).UpdateAsync(
            Arg.Is<User>(user => user.Name == "Member" && user.Role == UserRole.Admin),
            Arg.Any<CancellationToken>());
    }

    [Test]
    [Arguments("viewer")]
    [Arguments("editor")]
    public async Task Admin_CannotUseLegacyOrganizationRoles(string role)
    {
        var fixture = new TestFixture();
        fixture.SetupAsSystemAdmin("current@example.com");
        fixture.UserRepository.GetByIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(CreateUser(UserRole.Member));

        var result = await CreateHandler(fixture).Handle(
            new UpdateUserCommand(UserId, "Member", role, null),
            CancellationToken.None);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Invalid role. Must be 'admin' or 'member'");
    }

    private static User CreateUser(UserRole role) => new()
    {
        Id = UserId,
        Email = "member@example.com",
        Name = "Member",
        Role = role,
        Scope = KeyScope.All
    };

    private static UpdateUserCommandHandler CreateHandler(TestFixture fixture) => new(
        fixture.UserRepository,
        fixture.ProjectMemberRepository,
        fixture.DateTime,
        fixture.UserAuthorizationService);
}
