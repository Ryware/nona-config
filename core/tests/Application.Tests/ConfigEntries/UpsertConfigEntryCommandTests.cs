using Nona.Application.Admin.ConfigEntries.Commands;
using Nona.Application.Common;
using Nona.Application.Tests.Common;
using Nona.Domain;
using Nona.Domain.Entities;
using Nona.Domain.Enums;
using NSubstitute;

namespace Nona.Application.Tests.ConfigEntries;

public class UpsertConfigEntryCommandTests
{
    private const string ProjectName = "test-project";
    private const string EnvironmentName = "development";
    private const string ConfigKey = "Features:Checkout";
    private const string ConfigValue = "test-value";

    [Test]
    public async Task RejectsInvalidKeyBeforeAccessingRepositories()
    {
        var fixture = new TestFixture();
        var handler = new UpsertConfigEntryCommandHandler(
            fixture.ProjectRepository,
            fixture.EnvironmentRepository,
            fixture.ConfigEntryRepository,
            fixture.ProjectAccessService,
            fixture.DateTime);

        var result = await handler.Handle(
            new UpsertConfigEntryCommand(ProjectName, EnvironmentName, "feature/value", ConfigValue, null, null),
            CancellationToken.None);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo(ConfigEntryKey.ValidationError);
    }

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
        await Assert.That(result.ConfigEntry!.ContentType).IsEqualTo("text");
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
        await Assert.That(result.ErrorCode).IsEqualTo(AuthorizationErrorCodes.AccessDenied);
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

    [Test]
    public async Task UpsertConfigEntry_InfersContentType_WhenNotDeclared()
    {
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

        var command = new UpsertConfigEntryCommand(ProjectName, EnvironmentName, "max_items", "42", null, null);

        var result = await handler.Handle(command, CancellationToken.None);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.ConfigEntry!.ContentType).IsEqualTo("number");
    }

    [Test]
    public async Task UpsertConfigEntry_RejectsInvalidDeclaredContentTypeValue()
    {
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

        var command = new UpsertConfigEntryCommand(ProjectName, EnvironmentName, ConfigKey, "{bad", "json", null);

        var result = await handler.Handle(command, CancellationToken.None);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Value must be valid JSON when contentType is 'json'.");
    }

    [Test]
    public async Task UpsertConfigEntry_RejectsDeepHierarchyOnlyForNewKeys()
    {
        var fixture = new TestFixture();
        fixture.SetupAsSystemAdmin();
        fixture.SetupProjectExists(ProjectName);
        fixture.SetupEnvironmentExists(ProjectName, EnvironmentName);
        const string legacyKey = "one:two:three:four:five";

        var handler = new UpsertConfigEntryCommandHandler(
            fixture.ProjectRepository,
            fixture.EnvironmentRepository,
            fixture.ConfigEntryRepository,
            fixture.ProjectAccessService,
            fixture.DateTime);

        var createResult = await handler.Handle(
            new UpsertConfigEntryCommand(ProjectName, EnvironmentName, legacyKey, "value", "text", "all"),
            CancellationToken.None);

        await Assert.That(createResult.Success).IsFalse();
        await Assert.That(createResult.Error).IsEqualTo(ConfigEntryKey.HierarchyValidationError);

        fixture.ConfigEntryRepository.GetAsync(ProjectName, EnvironmentName, legacyKey, Arg.Any<CancellationToken>())
            .Returns(new ConfigEntry
            {
                Project = ProjectName,
                Environment = EnvironmentName,
                Key = legacyKey,
                Value = "old",
                ContentType = "text",
                Scope = KeyScope.All
            });

        var updateResult = await handler.Handle(
            new UpsertConfigEntryCommand(ProjectName, EnvironmentName, legacyKey, "new", null, null),
            CancellationToken.None);

        await Assert.That(updateResult.Success).IsTrue();
    }

    [Test]
    public async Task UpsertConfigEntry_VersionsDescriptionAndNumberUnit()
    {
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

        var result = await handler.Handle(
            new UpsertConfigEntryCommand(
                ProjectName,
                EnvironmentName,
                "Checkout:Timeout",
                "2500",
                "number",
                "client",
                "  Request timeout  ",
                " ms "),
            CancellationToken.None);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.ConfigEntry!.Description).IsEqualTo("Request timeout");
        await Assert.That(result.ConfigEntry.Unit).IsEqualTo("ms");
        await fixture.ConfigEntryRepository.Received(1).AddVersionAsync(
            Arg.Is<ConfigEntry>(entry => entry.Description == "Request timeout" && entry.Unit == "ms"),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }
}
