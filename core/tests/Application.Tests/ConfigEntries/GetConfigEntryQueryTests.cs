using Nona.Application.Admin.ConfigEntries.Queries;
using Nona.Application.Tests.Common;

namespace Nona.Application.Tests.ConfigEntries;

public class GetConfigEntryQueryTests
{
    private const string ProjectName = "test-project";
    private const string EnvironmentName = "development";
    private const string ConfigKey = "test-key";

    [Test]
    public async Task SystemAdmin_CanGetConfigEntry()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.SetupAsSystemAdmin();
        fixture.SetupProjectExists(ProjectName);
        fixture.SetupEnvironmentExists(ProjectName, EnvironmentName);
        fixture.SetupConfigEntryExists(ProjectName, EnvironmentName, ConfigKey);

        var handler = new GetConfigEntryQueryHandler(
            fixture.ProjectRepository,
            fixture.EnvironmentRepository,
            fixture.ConfigEntryRepository,
            fixture.ProjectAccessService);

        var query = new GetConfigEntryQuery(ProjectName, EnvironmentName, ConfigKey);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.ConfigEntry).IsNotNull();
        await Assert.That(result.ConfigEntry!.Key).IsEqualTo(ConfigKey);
    }

    [Test]
    public async Task ProjectAdmin_CanGetConfigEntry()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.SetupAsProjectAdmin("projectadmin", ProjectName);
        fixture.SetupProjectExists(ProjectName);
        fixture.SetupEnvironmentExists(ProjectName, EnvironmentName);
        fixture.SetupConfigEntryExists(ProjectName, EnvironmentName, ConfigKey);

        var handler = new GetConfigEntryQueryHandler(
            fixture.ProjectRepository,
            fixture.EnvironmentRepository,
            fixture.ConfigEntryRepository,
            fixture.ProjectAccessService);

        var query = new GetConfigEntryQuery(ProjectName, EnvironmentName, ConfigKey);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.ConfigEntry).IsNotNull();
    }

    [Test]
    public async Task ProjectUser_CanGetConfigEntry()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.SetupAsProjectUser("regularuser", ProjectName);
        fixture.SetupProjectExists(ProjectName);
        fixture.SetupEnvironmentExists(ProjectName, EnvironmentName);
        fixture.SetupConfigEntryExists(ProjectName, EnvironmentName, ConfigKey);

        var handler = new GetConfigEntryQueryHandler(
            fixture.ProjectRepository,
            fixture.EnvironmentRepository,
            fixture.ConfigEntryRepository,
            fixture.ProjectAccessService);

        var query = new GetConfigEntryQuery(ProjectName, EnvironmentName, ConfigKey);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.ConfigEntry).IsNotNull();
    }

    [Test]
    public async Task UserWithNoAccess_CannotGetConfigEntry()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.SetupAsUserWithNoProjectAccess("unauthorized", ProjectName);
        fixture.SetupProjectExists(ProjectName);
        fixture.SetupEnvironmentExists(ProjectName, EnvironmentName);
        fixture.SetupConfigEntryExists(ProjectName, EnvironmentName, ConfigKey);

        var handler = new GetConfigEntryQueryHandler(
            fixture.ProjectRepository,
            fixture.EnvironmentRepository,
            fixture.ConfigEntryRepository,
            fixture.ProjectAccessService);

        var query = new GetConfigEntryQuery(ProjectName, EnvironmentName, ConfigKey);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Access denied");
    }

    [Test]
    public async Task GetConfigEntry_ProjectNotFound_ReturnsFalse()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.SetupAsSystemAdmin();
        fixture.SetupProjectExists(ProjectName, exists: false);

        var handler = new GetConfigEntryQueryHandler(
            fixture.ProjectRepository,
            fixture.EnvironmentRepository,
            fixture.ConfigEntryRepository,
            fixture.ProjectAccessService);

        var query = new GetConfigEntryQuery(ProjectName, EnvironmentName, ConfigKey);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Project not found");
    }

    [Test]
    public async Task GetConfigEntry_ConfigEntryNotFound_ReturnsFalse()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.SetupAsSystemAdmin();
        fixture.SetupProjectExists(ProjectName);
        fixture.SetupEnvironmentExists(ProjectName, EnvironmentName);
        fixture.SetupConfigEntryExists(ProjectName, EnvironmentName, ConfigKey, exists: false);

        var handler = new GetConfigEntryQueryHandler(
            fixture.ProjectRepository,
            fixture.EnvironmentRepository,
            fixture.ConfigEntryRepository,
            fixture.ProjectAccessService);

        var query = new GetConfigEntryQuery(ProjectName, EnvironmentName, ConfigKey);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Config entry not found");
    }
}
