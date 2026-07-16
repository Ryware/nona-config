using Nona.Application.Admin.Projects.Commands;
using Nona.Application.Tests.Common;
using Nona.Domain.Entities;
using NSubstitute;

namespace Nona.Application.Tests.Projects;

public class CreateProjectCommandTests
{
    private const string ProjectName = "test-project";

    [Test]
    public async Task SystemAdmin_CanCreateProject()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.SetupAsSystemAdmin();
        fixture.SetupProjectExists(ProjectName, exists: false);

        var handler = new CreateProjectCommandHandler(
            fixture.ProjectRepository,
            fixture.UserAuthorizationService,
            fixture.EnvironmentRepository,
            fixture.Configuration,
            fixture.DateTime);

        var command = new CreateProjectCommand(ProjectName);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Project).IsNotNull();
        await Assert.That(result.Project!.Name).IsEqualTo(ProjectName);
        await Assert.That(result.Project.Environments).Contains("Production");
    }

    [Test]
    public async Task ProjectAdmin_CannotCreateProject()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.SetupAsProjectAdmin("projectadmin", "other-project");
        fixture.SetupProjectExists(ProjectName, exists: false);

        var handler = new CreateProjectCommandHandler(
            fixture.ProjectRepository,
            fixture.UserAuthorizationService,
            fixture.EnvironmentRepository,
            fixture.Configuration,
            fixture.DateTime);

        var command = new CreateProjectCommand(ProjectName);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Access denied. Only admin users can create projects.");
        await fixture.ProjectRepository.DidNotReceive()
            .AddAsync(Arg.Any<Nona.Domain.Entities.Project>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ProjectUser_CannotCreateProject()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.SetupAsProjectUser("regularuser", "other-project");
        fixture.SetupProjectExists(ProjectName, exists: false);

        var handler = new CreateProjectCommandHandler(
            fixture.ProjectRepository,
            fixture.UserAuthorizationService,
            fixture.EnvironmentRepository,
            fixture.Configuration,
            fixture.DateTime);

        var command = new CreateProjectCommand(ProjectName);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Access denied. Only admin users can create projects.");
    }

    [Test]
    public async Task CreateProject_ProjectAlreadyExists_ReturnsFalse()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.SetupAsSystemAdmin();
        fixture.SetupProjectExists(ProjectName, exists: true);

        var handler = new CreateProjectCommandHandler(
            fixture.ProjectRepository,
            fixture.UserAuthorizationService,
            fixture.EnvironmentRepository,
            fixture.Configuration,
            fixture.DateTime);

        var command = new CreateProjectCommand(ProjectName);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Project already exists");
    }

    [Test]
    public async Task SystemAdmin_IsAuthorizedFromPersistedUser()
    {
        var fixture = new TestFixture();
        fixture.CurrentUserService.IsAdmin.Returns(false);
        fixture.CurrentUserService.Role.Returns(UserRole.Viewer);
        fixture.UserAuthorizationService.GetCurrentUserAsync(Arg.Any<CancellationToken>())
            .Returns(new User
            {
                Email = "admin@example.com",
                Name = "Admin",
                IsAdmin = true,
                Role = UserRole.Viewer
            });
        fixture.SetupProjectExists(ProjectName, exists: false);

        var handler = new CreateProjectCommandHandler(
            fixture.ProjectRepository,
            fixture.UserAuthorizationService,
            fixture.EnvironmentRepository,
            fixture.Configuration,
            fixture.DateTime);

        var result = await handler.Handle(new CreateProjectCommand(ProjectName), CancellationToken.None);

        await Assert.That(result.Success).IsTrue();
        await fixture.ProjectRepository.Received(1)
            .AddAsync(Arg.Any<Project>(), Arg.Any<CancellationToken>());
    }
}
