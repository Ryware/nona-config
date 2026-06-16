using Nona.Application.Admin.ConfigEntries.Commands;
using Nona.Application.Tests.Common;

namespace Nona.Application.Tests.ConfigEntries;

public class UpsertConfigEntryCommandTests
{
    private const string ProjectName = "test-project";
    private const string EnvironmentName = "development";
    private const string ConfigKey = "test-key";
    private const string ConfigValue = "test-value";

    [Test]
    public async Task SystemAdmin_CanUpsertConfigEntry()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.SetupAsSystemAdmin();
        fixture.SetupProjectExists(ProjectName);
        fixture.SetupEnvironmentExists(ProjectName, EnvironmentName);

        var handler = new UpsertConfigEntryCommandHandler(
            fixture.ProjectRepository,
            fixture.EnvironmentRepository,
            fixture.ConfigEntryRepository,
            fixture.ProjectAccessService,
            fixture.DateTime);

        var command = new UpsertConfigEntryCommand(ProjectName, EnvironmentName, ConfigKey, ConfigValue, null, null);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.ConfigEntry).IsNotNull();
        await Assert.That(result.ConfigEntry!.Key).IsEqualTo(ConfigKey);
        await Assert.That(result.ConfigEntry!.Value).IsEqualTo(ConfigValue);
    }

    [Test]
    public async Task ProjectAdmin_CanUpsertConfigEntry()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.SetupAsProjectAdmin("projectadmin", ProjectName);
        fixture.SetupProjectExists(ProjectName);
        fixture.SetupEnvironmentExists(ProjectName, EnvironmentName);

        var handler = new UpsertConfigEntryCommandHandler(
            fixture.ProjectRepository,
            fixture.EnvironmentRepository,
            fixture.ConfigEntryRepository,
            fixture.ProjectAccessService,
            fixture.DateTime);

        var command = new UpsertConfigEntryCommand(ProjectName, EnvironmentName, ConfigKey, ConfigValue, null, null);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.ConfigEntry).IsNotNull();
    }

    [Test]
    public async Task ProjectUser_CannotUpsertConfigEntry()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.SetupAsProjectUser("regularuser", ProjectName);
        fixture.SetupProjectExists(ProjectName);
        fixture.SetupEnvironmentExists(ProjectName, EnvironmentName);

        var handler = new UpsertConfigEntryCommandHandler(
            fixture.ProjectRepository,
            fixture.EnvironmentRepository,
            fixture.ConfigEntryRepository,
            fixture.ProjectAccessService,
            fixture.DateTime);

        var command = new UpsertConfigEntryCommand(ProjectName, EnvironmentName, ConfigKey, ConfigValue, null, null);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Access denied");
    }

    [Test]
    public async Task UserWithNoAccess_CannotUpsertConfigEntry()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.SetupAsUserWithNoProjectAccess("unauthorized", ProjectName);
        fixture.SetupProjectExists(ProjectName);
        fixture.SetupEnvironmentExists(ProjectName, EnvironmentName);

        var handler = new UpsertConfigEntryCommandHandler(
            fixture.ProjectRepository,
            fixture.EnvironmentRepository,
            fixture.ConfigEntryRepository,
            fixture.ProjectAccessService,
            fixture.DateTime);

        var command = new UpsertConfigEntryCommand(ProjectName, EnvironmentName, ConfigKey, ConfigValue, null, null);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Access denied");
    }

    [Test]
    public async Task UpsertConfigEntry_ProjectNotFound_ReturnsFalse()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.SetupAsSystemAdmin();
        fixture.SetupProjectExists(ProjectName, exists: false);

        var handler = new UpsertConfigEntryCommandHandler(
            fixture.ProjectRepository,
            fixture.EnvironmentRepository,
            fixture.ConfigEntryRepository,
            fixture.ProjectAccessService,
            fixture.DateTime);

        var command = new UpsertConfigEntryCommand(ProjectName, EnvironmentName, ConfigKey, ConfigValue, null, null);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Project not found");
    }

    [Test]
    public async Task UpsertConfigEntry_EnvironmentNotFound_ReturnsFalse()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.SetupAsSystemAdmin();
        fixture.SetupProjectExists(ProjectName);
        fixture.SetupEnvironmentExists(ProjectName, EnvironmentName, exists: false);

        var handler = new UpsertConfigEntryCommandHandler(
            fixture.ProjectRepository,
            fixture.EnvironmentRepository,
            fixture.ConfigEntryRepository,
            fixture.ProjectAccessService,
            fixture.DateTime);

        var command = new UpsertConfigEntryCommand(ProjectName, EnvironmentName, ConfigKey, ConfigValue, null, null);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Environment not found");
    }
}
