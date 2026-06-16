using Nona.Application.Admin.Projects.Commands;
using Nona.Application.Tests.Common;
using Nona.Domain.Entities;
using NSubstitute;

namespace Nona.Application.Tests.Projects;

public class DeleteProjectCommandTests
{
    private const string ProjectName = "test-project";

    [Test]
    public async Task SystemAdmin_CanDeleteProject()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.SetupAsSystemAdmin();
        fixture.SetupProjectExists(ProjectName);
        fixture.EnvironmentRepository.ListByProjectAsync(ProjectName, Arg.Any<CancellationToken>())
            .Returns(new List<ProjectEnvironment>());
        fixture.ConfigEntryRepository.ListByProjectAsync(ProjectName, Arg.Any<CancellationToken>())
            .Returns(new List<ConfigEntry>());

        var handler = new DeleteProjectCommandHandler(
            fixture.ProjectRepository,
            fixture.EnvironmentRepository,
            fixture.ConfigEntryRepository,
            fixture.ProjectMemberRepository,
            fixture.UserAuthorizationService);

        var command = new DeleteProjectCommand(ProjectName);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsTrue();
        await fixture.ProjectRepository.Received(1).DeleteAsync(ProjectName, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ProjectAdmin_CannotDeleteProject()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.SetupAsProjectAdmin("projectadmin", ProjectName);
        fixture.SetupProjectExists(ProjectName);

        var handler = new DeleteProjectCommandHandler(
            fixture.ProjectRepository,
            fixture.EnvironmentRepository,
            fixture.ConfigEntryRepository,
            fixture.ProjectMemberRepository,
            fixture.UserAuthorizationService);

        var command = new DeleteProjectCommand(ProjectName);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Access denied. Only admin users can delete projects.");
        await fixture.ProjectRepository.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ProjectUser_CannotDeleteProject()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.SetupAsProjectUser("regularuser", ProjectName);
        fixture.SetupProjectExists(ProjectName);

        var handler = new DeleteProjectCommandHandler(
            fixture.ProjectRepository,
            fixture.EnvironmentRepository,
            fixture.ConfigEntryRepository,
            fixture.ProjectMemberRepository,
            fixture.UserAuthorizationService);

        var command = new DeleteProjectCommand(ProjectName);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Access denied. Only admin users can delete projects.");
    }

    [Test]
    public async Task UserWithNoAccess_CannotDeleteProject()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.SetupAsUserWithNoProjectAccess("unauthorized", ProjectName);
        fixture.SetupProjectExists(ProjectName);

        var handler = new DeleteProjectCommandHandler(
            fixture.ProjectRepository,
            fixture.EnvironmentRepository,
            fixture.ConfigEntryRepository,
            fixture.ProjectMemberRepository,
            fixture.UserAuthorizationService);

        var command = new DeleteProjectCommand(ProjectName);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Access denied. Only admin users can delete projects.");
    }

    [Test]
    public async Task DeleteProject_ProjectNotFound_ReturnsFalse()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.SetupAsSystemAdmin();
        fixture.SetupProjectExists(ProjectName, exists: false);

        var handler = new DeleteProjectCommandHandler(
            fixture.ProjectRepository,
            fixture.EnvironmentRepository,
            fixture.ConfigEntryRepository,
            fixture.ProjectMemberRepository,
            fixture.UserAuthorizationService);

        var command = new DeleteProjectCommand(ProjectName);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Project not found");
    }
}
