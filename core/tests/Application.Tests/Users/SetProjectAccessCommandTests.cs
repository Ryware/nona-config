using Nona.Application.Admin.Users.Commands;
using Nona.Application.Admin.Users.Validators;
using Nona.Application.Tests.Common;
using Nona.Domain.Entities;
using NSubstitute;

namespace Nona.Application.Tests.Users;

public class SetProjectAccessCommandTests
{
    private const long UserId = 42;
    private const string Email = "user@example.com";
    private const string ProjectName = "alpha";

    [Test]
    public async Task SetProjectAccess_AllowsEditorRole()
    {
        var fixture = new TestFixture();
        fixture.CurrentUserService.Role.Returns(UserRole.Editor);
        fixture.UserAuthorizationService.GetCurrentUserAsync(Arg.Any<CancellationToken>())
            .Returns(new User { Id = 7, Email = "editor@example.com", Name = "Editor", Role = UserRole.Editor });
        fixture.UserRepository.GetByIdAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new User
            {
                Id = UserId,
                Email = Email,
                Name = "User"
            });
        fixture.ProjectRepository.GetByNameAsync(ProjectName, Arg.Any<CancellationToken>())
            .Returns(new Project { Name = ProjectName });

        var handler = new SetProjectAccessCommandHandler(
            fixture.UserRepository,
            fixture.ProjectRepository,
            fixture.ProjectMemberRepository,
            fixture.UserAuthorizationService);

        var result = await handler.Handle(
            new SetProjectAccessCommand(UserId, ProjectName, "editor"),
            CancellationToken.None);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.ProjectAccess).IsNotNull();
        await Assert.That(result.ProjectAccess!.ProjectName).IsEqualTo(ProjectName);
        await Assert.That(result.ProjectAccess.Role).IsEqualTo("editor");
        await fixture.ProjectMemberRepository.Received(1).AddAsync(
            Arg.Is<ProjectMember>(member =>
                member.Username == Email &&
                member.ProjectId == ProjectName &&
                member.Role == ProjectRole.Editor),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SetProjectAccess_RejectsViewer()
    {
        var fixture = new TestFixture();
        fixture.CurrentUserService.Role.Returns(UserRole.Viewer);
        fixture.UserAuthorizationService.GetCurrentUserAsync(Arg.Any<CancellationToken>())
            .Returns(new User { Id = 7, Email = "viewer@example.com", Name = "Viewer", Role = UserRole.Viewer });

        var handler = new SetProjectAccessCommandHandler(
            fixture.UserRepository,
            fixture.ProjectRepository,
            fixture.ProjectMemberRepository,
            fixture.UserAuthorizationService);

        var result = await handler.Handle(
            new SetProjectAccessCommand(UserId, ProjectName, "editor"),
            CancellationToken.None);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Access denied");
        await fixture.ProjectMemberRepository.DidNotReceive().AddAsync(
            Arg.Any<ProjectMember>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SetProjectAccess_UsesPersistedRoleAfterSelfDemotion()
    {
        var fixture = new TestFixture();
        fixture.CurrentUserService.Role.Returns(UserRole.Editor);
        fixture.UserAuthorizationService.GetCurrentUserAsync(Arg.Any<CancellationToken>())
            .Returns(new User { Id = 7, Email = "viewer@example.com", Name = "Former Editor", Role = UserRole.Viewer });

        var handler = new SetProjectAccessCommandHandler(
            fixture.UserRepository,
            fixture.ProjectRepository,
            fixture.ProjectMemberRepository,
            fixture.UserAuthorizationService);

        var result = await handler.Handle(
            new SetProjectAccessCommand(UserId, ProjectName, "editor"),
            CancellationToken.None);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Access denied");
        await fixture.ProjectMemberRepository.DidNotReceive().AddAsync(
            Arg.Any<ProjectMember>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SetProjectAccess_RejectsEditorModifyingSystemAdmin()
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
                IsAdmin = true
            });

        var handler = new SetProjectAccessCommandHandler(
            fixture.UserRepository,
            fixture.ProjectRepository,
            fixture.ProjectMemberRepository,
            fixture.UserAuthorizationService);

        var result = await handler.Handle(
            new SetProjectAccessCommand(UserId, ProjectName, "viewer"),
            CancellationToken.None);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Access denied");
        await fixture.ProjectMemberRepository.DidNotReceive().AddAsync(
            Arg.Any<ProjectMember>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SetProjectAccess_StoresCanonicalProjectNameWhenSlugIsProvided()
    {
        var fixture = new TestFixture();
        const string slug = "alpha-slug";
        fixture.UserAuthorizationService.GetCurrentUserAsync(Arg.Any<CancellationToken>())
            .Returns(new User { Id = 7, Email = "editor@example.com", Name = "Editor", Role = UserRole.Editor });
        fixture.UserRepository.GetByIdAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new User
            {
                Id = UserId,
                Email = Email,
                Name = "User"
            });
        fixture.ProjectRepository.GetByNameAsync(slug, Arg.Any<CancellationToken>()).Returns((Project?)null);
        fixture.ProjectRepository.ListAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Project> { new() { Id = 100, Name = ProjectName, UrlSlug = slug } });

        var handler = new SetProjectAccessCommandHandler(
            fixture.UserRepository,
            fixture.ProjectRepository,
            fixture.ProjectMemberRepository,
            fixture.UserAuthorizationService);

        var result = await handler.Handle(
            new SetProjectAccessCommand(UserId, slug, "viewer"),
            CancellationToken.None);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.ProjectAccess!.ProjectName).IsEqualTo(ProjectName);
        await fixture.ProjectMemberRepository.Received(1).AddAsync(
            Arg.Is<ProjectMember>(member =>
                member.Username == Email &&
                member.ProjectId == ProjectName &&
                member.Role == ProjectRole.Viewer),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Validator_AcceptsViewerAndEditorRoles()
    {
        var validator = new ProjectAccessRequestValidator();

        var viewer = await validator.ValidateAsync(new ProjectAccessRequest("viewer"));
        var editor = await validator.ValidateAsync(new ProjectAccessRequest("editor"));

        await Assert.That(viewer.IsValid).IsTrue();
        await Assert.That(editor.IsValid).IsTrue();
    }

    [Test]
    public async Task Validator_RejectsAdminRole()
    {
        var validator = new ProjectAccessRequestValidator();

        var result = await validator.ValidateAsync(new ProjectAccessRequest("admin"));

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors.Select(error => error.ErrorMessage)).Contains("Role must be 'viewer' or 'editor'");
    }
}
