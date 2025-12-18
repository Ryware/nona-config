using Nona.Application.Admin.Environments.Commands;
using Nona.Application.Tests.Common;
using Nona.Domain.Entities;
using NSubstitute;

namespace Nona.Application.Tests.Environments;

public class DeleteEnvironmentCommandTests
{
    private const string ProjectName = "test-project";
    private const string EnvironmentName = "development";

    [Test]
    public async Task SystemAdmin_CanDeleteEnvironment()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.SetupAsSystemAdmin();
        fixture.SetupProjectExists(ProjectName);
        fixture.SetupEnvironmentExists(ProjectName, EnvironmentName);
        fixture.ConfigEntryRepository.ListAsync(ProjectName, EnvironmentName, Arg.Any<CancellationToken>())
            .Returns(new List<ConfigEntry>());

        var handler = new DeleteEnvironmentCommandHandler(
            fixture.ProjectRepository,
            fixture.EnvironmentRepository,
            fixture.ConfigEntryRepository,
            fixture.ProjectAccessService);

        var command = new DeleteEnvironmentCommand(ProjectName, EnvironmentName);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsTrue();
        await fixture.EnvironmentRepository.Received(1)
            .DeleteAsync(ProjectName, EnvironmentName, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ProjectAdmin_CanDeleteEnvironment()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.SetupAsProjectAdmin("projectadmin", ProjectName);
        fixture.SetupProjectExists(ProjectName);
        fixture.SetupEnvironmentExists(ProjectName, EnvironmentName);
        fixture.ConfigEntryRepository.ListAsync(ProjectName, EnvironmentName, Arg.Any<CancellationToken>())
            .Returns(new List<ConfigEntry>());

        var handler = new DeleteEnvironmentCommandHandler(
            fixture.ProjectRepository,
            fixture.EnvironmentRepository,
            fixture.ConfigEntryRepository,
            fixture.ProjectAccessService);

        var command = new DeleteEnvironmentCommand(ProjectName, EnvironmentName);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsTrue();
        await fixture.EnvironmentRepository.Received(1)
            .DeleteAsync(ProjectName, EnvironmentName, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ProjectUser_CannotDeleteEnvironment()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.SetupAsProjectUser("regularuser", ProjectName);
        fixture.SetupProjectExists(ProjectName);
        fixture.SetupEnvironmentExists(ProjectName, EnvironmentName);

        var handler = new DeleteEnvironmentCommandHandler(
            fixture.ProjectRepository,
            fixture.EnvironmentRepository,
            fixture.ConfigEntryRepository,
            fixture.ProjectAccessService);

        var command = new DeleteEnvironmentCommand(ProjectName, EnvironmentName);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Access denied");
        await fixture.EnvironmentRepository.DidNotReceive()
            .DeleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task UserWithNoAccess_CannotDeleteEnvironment()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.SetupAsUserWithNoProjectAccess("unauthorized", ProjectName);
        fixture.SetupProjectExists(ProjectName);
        fixture.SetupEnvironmentExists(ProjectName, EnvironmentName);

        var handler = new DeleteEnvironmentCommandHandler(
            fixture.ProjectRepository,
            fixture.EnvironmentRepository,
            fixture.ConfigEntryRepository,
            fixture.ProjectAccessService);

        var command = new DeleteEnvironmentCommand(ProjectName, EnvironmentName);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Access denied");
    }

    [Test]
    public async Task DeleteEnvironment_ProjectNotFound_ReturnsFalse()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.SetupAsSystemAdmin();
        fixture.SetupProjectExists(ProjectName, exists: false);

        var handler = new DeleteEnvironmentCommandHandler(
            fixture.ProjectRepository,
            fixture.EnvironmentRepository,
            fixture.ConfigEntryRepository,
            fixture.ProjectAccessService);

        var command = new DeleteEnvironmentCommand(ProjectName, EnvironmentName);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Project not found");
    }

    [Test]
    public async Task DeleteEnvironment_EnvironmentNotFound_ReturnsFalse()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.SetupAsSystemAdmin();
        fixture.SetupProjectExists(ProjectName);
        fixture.SetupEnvironmentExists(ProjectName, EnvironmentName, exists: false);

        var handler = new DeleteEnvironmentCommandHandler(
            fixture.ProjectRepository,
            fixture.EnvironmentRepository,
            fixture.ConfigEntryRepository,
            fixture.ProjectAccessService);

        var command = new DeleteEnvironmentCommand(ProjectName, EnvironmentName);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Environment not found");
    }
}
