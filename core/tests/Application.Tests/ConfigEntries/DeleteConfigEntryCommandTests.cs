using Nona.Application.Admin.ConfigEntries.Commands;
using Nona.Application.Tests.Common;
using NSubstitute;

namespace Nona.Application.Tests.ConfigEntries;

public class DeleteConfigEntryCommandTests
{
    private const string ProjectName = "test-project";
    private const string EnvironmentName = "development";
    private const string ConfigKey = "test-key";

    [Test]
    public async Task SystemAdmin_CanDeleteConfigEntry()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.SetupAsSystemAdmin();
        fixture.SetupProjectExists(ProjectName);
        fixture.SetupEnvironmentExists(ProjectName, EnvironmentName);
        fixture.SetupConfigEntryExists(ProjectName, EnvironmentName, ConfigKey);

        var handler = new DeleteConfigEntryCommandHandler(
            fixture.ProjectRepository,
            fixture.EnvironmentRepository,
            fixture.ConfigEntryRepository,
            fixture.ProjectAccessService);

        var command = new DeleteConfigEntryCommand(ProjectName, EnvironmentName, ConfigKey);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsTrue();
        await fixture.ConfigEntryRepository.Received(1)
            .DeleteAsync(ProjectName, EnvironmentName, ConfigKey, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ProjectAdmin_CanDeleteConfigEntry()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.SetupAsProjectAdmin("projectadmin", ProjectName);
        fixture.SetupProjectExists(ProjectName);
        fixture.SetupEnvironmentExists(ProjectName, EnvironmentName);
        fixture.SetupConfigEntryExists(ProjectName, EnvironmentName, ConfigKey);

        var handler = new DeleteConfigEntryCommandHandler(
            fixture.ProjectRepository,
            fixture.EnvironmentRepository,
            fixture.ConfigEntryRepository,
            fixture.ProjectAccessService);

        var command = new DeleteConfigEntryCommand(ProjectName, EnvironmentName, ConfigKey);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsTrue();
        await fixture.ConfigEntryRepository.Received(1)
            .DeleteAsync(ProjectName, EnvironmentName, ConfigKey, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ProjectUser_CannotDeleteConfigEntry()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.SetupAsProjectUser("regularuser", ProjectName);
        fixture.SetupProjectExists(ProjectName);
        fixture.SetupEnvironmentExists(ProjectName, EnvironmentName);
        fixture.SetupConfigEntryExists(ProjectName, EnvironmentName, ConfigKey);

        var handler = new DeleteConfigEntryCommandHandler(
            fixture.ProjectRepository,
            fixture.EnvironmentRepository,
            fixture.ConfigEntryRepository,
            fixture.ProjectAccessService);

        var command = new DeleteConfigEntryCommand(ProjectName, EnvironmentName, ConfigKey);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Access denied");
        await fixture.ConfigEntryRepository.DidNotReceive()
            .DeleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task UserWithNoAccess_CannotDeleteConfigEntry()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.SetupAsUserWithNoProjectAccess("unauthorized", ProjectName);
        fixture.SetupProjectExists(ProjectName);
        fixture.SetupEnvironmentExists(ProjectName, EnvironmentName);
        fixture.SetupConfigEntryExists(ProjectName, EnvironmentName, ConfigKey);

        var handler = new DeleteConfigEntryCommandHandler(
            fixture.ProjectRepository,
            fixture.EnvironmentRepository,
            fixture.ConfigEntryRepository,
            fixture.ProjectAccessService);

        var command = new DeleteConfigEntryCommand(ProjectName, EnvironmentName, ConfigKey);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Access denied");
    }

    [Test]
    public async Task DeleteConfigEntry_ProjectNotFound_ReturnsFalse()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.SetupAsSystemAdmin();
        fixture.SetupProjectExists(ProjectName, exists: false);

        var handler = new DeleteConfigEntryCommandHandler(
            fixture.ProjectRepository,
            fixture.EnvironmentRepository,
            fixture.ConfigEntryRepository,
            fixture.ProjectAccessService);

        var command = new DeleteConfigEntryCommand(ProjectName, EnvironmentName, ConfigKey);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Project not found");
    }

    [Test]
    public async Task DeleteConfigEntry_ConfigEntryNotFound_ReturnsFalse()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.SetupAsSystemAdmin();
        fixture.SetupProjectExists(ProjectName);
        fixture.SetupEnvironmentExists(ProjectName, EnvironmentName);
        fixture.SetupConfigEntryExists(ProjectName, EnvironmentName, ConfigKey, exists: false);

        var handler = new DeleteConfigEntryCommandHandler(
            fixture.ProjectRepository,
            fixture.EnvironmentRepository,
            fixture.ConfigEntryRepository,
            fixture.ProjectAccessService);

        var command = new DeleteConfigEntryCommand(ProjectName, EnvironmentName, ConfigKey);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Config entry not found");
    }
}
