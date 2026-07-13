using Nona.Application.Admin.ConfigEntries.Commands;
using Nona.Application.Admin.ConfigEntries.Queries;
using Nona.Application.Tests.Common;
using Nona.Domain.Entities;
using Nona.Domain.Enums;
using NSubstitute;

namespace Nona.Application.Tests.ConfigEntries;

public class ConfigEntryVersionHistoryTests
{
    private const string ProjectName = "test-project";
    private const string EnvironmentName = "development";
    private const string ConfigKey = "feature.enabled";

    [Test]
    public async Task UpsertConfigEntry_CreatesVersionWithActorAndReturnsActiveVersion()
    {
        var fixture = new TestFixture();
        var now = new DateTime(2026, 6, 22, 10, 0, 0, DateTimeKind.Utc);
        fixture.DateTime.NowUtc.Returns(now);
        fixture.SetupAsSystemAdmin("alice");
        fixture.SetupProjectExists(ProjectName);
        fixture.SetupEnvironmentExists(ProjectName, EnvironmentName);

        var handler = new UpsertConfigEntryCommandHandler(
            fixture.ProjectRepository,
            fixture.EnvironmentRepository,
            fixture.ConfigEntryRepository,
            fixture.ProjectAccessService,
            fixture.DateTime,
            currentUserService: fixture.CurrentUserService);

        var result = await handler.Handle(
            new UpsertConfigEntryCommand(ProjectName, EnvironmentName, ConfigKey, "true", "boolean", "client"),
            CancellationToken.None);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.ConfigEntry!.ActiveVersion).IsEqualTo(1);

        await fixture.ConfigEntryRepository.Received(1).AddVersionAsync(
            Arg.Is<ConfigEntry>(entry =>
                entry.Project == ProjectName
                && entry.Environment == EnvironmentName
                && entry.Key == ConfigKey
                && entry.Value == "true"
                && entry.ContentType == "boolean"
                && entry.Scope == KeyScope.Frontend
                && entry.CreatedAt == now
                && entry.UpdatedAt == now),
            "alice",
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task UpsertConfigEntry_WhenExistingEntry_CreatesNewVersionInsteadOfMutating()
    {
        var fixture = new TestFixture();
        var createdAt = new DateTime(2026, 6, 21, 10, 0, 0, DateTimeKind.Utc);
        var updatedAt = new DateTime(2026, 6, 22, 10, 0, 0, DateTimeKind.Utc);
        fixture.DateTime.NowUtc.Returns(updatedAt);
        fixture.SetupAsSystemAdmin("alice");
        fixture.SetupProjectExists(ProjectName);
        fixture.SetupEnvironmentExists(ProjectName, EnvironmentName);
        fixture.ConfigEntryRepository.GetAsync(ProjectName, EnvironmentName, ConfigKey, Arg.Any<CancellationToken>())
            .Returns(new ConfigEntry
            {
                Project = ProjectName,
                Environment = EnvironmentName,
                Key = ConfigKey,
                Value = "old-value",
                ContentType = "text",
                Scope = KeyScope.Backend,
                ActiveVersion = 1,
                CreatedAt = createdAt,
                UpdatedAt = createdAt
            });
        fixture.ConfigEntryRepository.AddVersionAsync(Arg.Any<ConfigEntry>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var entry = call.ArgAt<ConfigEntry>(0);
                return new ConfigEntry
                {
                    Project = entry.Project,
                    Environment = entry.Environment,
                    Key = entry.Key,
                    Value = entry.Value,
                    ContentType = entry.ContentType,
                    Scope = entry.Scope,
                    ActiveVersion = 2,
                    CreatedAt = entry.CreatedAt,
                    UpdatedAt = entry.UpdatedAt
                };
            });

        var handler = new UpsertConfigEntryCommandHandler(
            fixture.ProjectRepository,
            fixture.EnvironmentRepository,
            fixture.ConfigEntryRepository,
            fixture.ProjectAccessService,
            fixture.DateTime,
            currentUserService: fixture.CurrentUserService);

        var result = await handler.Handle(
            new UpsertConfigEntryCommand(ProjectName, EnvironmentName, ConfigKey, "new-value", null, null),
            CancellationToken.None);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.ConfigEntry!.ActiveVersion).IsEqualTo(2);
        await Assert.That(result.ConfigEntry!.Value).IsEqualTo("new-value");

        await fixture.ConfigEntryRepository.Received(1).AddVersionAsync(
            Arg.Is<ConfigEntry>(entry =>
                entry.Value == "new-value"
                && entry.ContentType == "text"
                && entry.Scope == KeyScope.Backend
                && entry.CreatedAt == createdAt
                && entry.UpdatedAt == updatedAt),
            "alice",
            Arg.Any<CancellationToken>());
        await fixture.ConfigEntryRepository.DidNotReceive().UpdateAsync(Arg.Any<ConfigEntry>(), Arg.Any<CancellationToken>());
        await fixture.ConfigEntryRepository.DidNotReceive().UpsertAsync(Arg.Any<ConfigEntry>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ListConfigEntryVersions_ReturnsVersionSnapshots()
    {
        var fixture = new TestFixture();
        fixture.SetupAsSystemAdmin();
        fixture.SetupProjectExists(ProjectName);
        fixture.SetupEnvironmentExists(ProjectName, EnvironmentName);
        fixture.SetupConfigEntryExists(ProjectName, EnvironmentName, ConfigKey);
        fixture.ConfigEntryRepository.ListVersionsAsync(ProjectName, EnvironmentName, ConfigKey, Arg.Any<CancellationToken>())
            .Returns(new List<ConfigEntryVersion>
            {
                new()
                {
                    Project = ProjectName,
                    Environment = EnvironmentName,
                    Key = ConfigKey,
                    Version = 2,
                    Value = "new-value",
                    ContentType = "text",
                    Scope = KeyScope.All,
                    CreatedAt = new DateTime(2026, 6, 22, 10, 0, 0, DateTimeKind.Utc),
                    Actor = "bob"
                },
                new()
                {
                    Project = ProjectName,
                    Environment = EnvironmentName,
                    Key = ConfigKey,
                    Version = 1,
                    Value = "old-value",
                    ContentType = "json",
                    Scope = KeyScope.Backend,
                    CreatedAt = new DateTime(2026, 6, 21, 10, 0, 0, DateTimeKind.Utc),
                    Actor = "alice"
                }
            });

        var handler = new ListConfigEntryVersionsQueryHandler(
            fixture.ProjectRepository,
            fixture.EnvironmentRepository,
            fixture.ConfigEntryRepository,
            fixture.ProjectAccessService);

        var result = await handler.Handle(
            new ListConfigEntryVersionsQuery(ProjectName, EnvironmentName, ConfigKey),
            CancellationToken.None);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Versions).Count().IsEqualTo(2);
        await Assert.That(result.Versions![0].Version).IsEqualTo(2);
        await Assert.That(result.Versions![0].Actor).IsEqualTo("bob");
        await Assert.That(result.Versions![1].Value).IsEqualTo("old-value");
        await Assert.That(result.Versions![1].ContentType).IsEqualTo("json");
        await Assert.That(result.Versions![1].Scope).IsEqualTo("server");
    }

    [Test]
    public async Task RollbackConfigEntry_CreatesNewVersionFromTargetSnapshot()
    {
        var fixture = new TestFixture();
        var originalCreatedAt = new DateTime(2026, 6, 20, 10, 0, 0, DateTimeKind.Utc);
        var rollbackAt = new DateTime(2026, 6, 22, 10, 0, 0, DateTimeKind.Utc);
        fixture.DateTime.NowUtc.Returns(rollbackAt);
        fixture.SetupAsSystemAdmin("carol");
        fixture.SetupProjectExists(ProjectName);
        fixture.SetupEnvironmentExists(ProjectName, EnvironmentName);
        fixture.ConfigEntryRepository.GetAsync(ProjectName, EnvironmentName, ConfigKey, Arg.Any<CancellationToken>())
            .Returns(new ConfigEntry
            {
                Project = ProjectName,
                Environment = EnvironmentName,
                Key = ConfigKey,
                Value = "bad-value",
                ContentType = "text",
                Scope = KeyScope.All,
                ActiveVersion = 2,
                CreatedAt = originalCreatedAt,
                UpdatedAt = rollbackAt.AddMinutes(-5)
            });
        fixture.ConfigEntryRepository.GetVersionAsync(ProjectName, EnvironmentName, ConfigKey, 1, Arg.Any<CancellationToken>())
            .Returns(new ConfigEntryVersion
            {
                Project = ProjectName,
                Environment = EnvironmentName,
                Key = ConfigKey,
                Version = 1,
                Value = "good-value",
                ContentType = "json",
                Scope = KeyScope.Backend,
                CreatedAt = originalCreatedAt,
                Actor = "alice"
            });
        fixture.ConfigEntryRepository.AddVersionAsync(Arg.Any<ConfigEntry>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var entry = call.ArgAt<ConfigEntry>(0);
                return new ConfigEntry
                {
                    Project = entry.Project,
                    Environment = entry.Environment,
                    Key = entry.Key,
                    Value = entry.Value,
                    ContentType = entry.ContentType,
                    Scope = entry.Scope,
                    ActiveVersion = 3,
                    CreatedAt = entry.CreatedAt,
                    UpdatedAt = entry.UpdatedAt
                };
            });

        var handler = new RollbackConfigEntryCommandHandler(
            fixture.ProjectRepository,
            fixture.EnvironmentRepository,
            fixture.ConfigEntryRepository,
            fixture.ProjectAccessService,
            fixture.DateTime,
            currentUserService: fixture.CurrentUserService);

        var result = await handler.Handle(
            new RollbackConfigEntryCommand(ProjectName, EnvironmentName, ConfigKey, 1),
            CancellationToken.None);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.ConfigEntry!.ActiveVersion).IsEqualTo(3);
        await Assert.That(result.ConfigEntry!.Value).IsEqualTo("good-value");

        await fixture.ConfigEntryRepository.Received(1).AddVersionAsync(
            Arg.Is<ConfigEntry>(entry =>
                entry.Value == "good-value"
                && entry.ContentType == "json"
                && entry.Scope == KeyScope.Backend
                && entry.CreatedAt == originalCreatedAt
                && entry.UpdatedAt == rollbackAt),
            "carol",
            Arg.Any<CancellationToken>());
    }
}
