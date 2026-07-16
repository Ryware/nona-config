using Nona.Application.Admin.ConfigReleases.Commands;
using Nona.Application.Tests.Common;
using Nona.Domain.Entities;
using Nona.Domain.Enums;
using NSubstitute;

namespace Nona.Application.Tests.ConfigReleases;

public class ConfigReleaseCommandTests
{
    private const string ProjectName = "test-project";
    private const string EnvironmentName = "production";

    [Test]
    public async Task PublishConfigRelease_SnapshotsWorkingConfiguration()
    {
        var fixture = new TestFixture();
        fixture.SetupAsSystemAdmin("alice");
        fixture.SetupProjectExists(ProjectName);
        fixture.SetupEnvironmentExists(ProjectName, EnvironmentName);
        fixture.DateTime.NowUtc.Returns(new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc));
        fixture.ConfigEntryRepository.ListAsync(ProjectName, EnvironmentName, Arg.Any<CancellationToken>())
            .Returns(
            [
                new ConfigEntry
                {
                    Project = ProjectName,
                    Environment = EnvironmentName,
                    Key = "feature.enabled",
                    Value = "true",
                    ContentType = "boolean",
                    Scope = KeyScope.Frontend
                },
                new ConfigEntry
                {
                    Project = ProjectName,
                    Environment = EnvironmentName,
                    Key = "settings",
                    Value = """{"theme":"light"}""",
                    ContentType = "json",
                    Scope = KeyScope.All
                }
            ]);
        fixture.ConfigReleaseRepository.AddAsync(Arg.Any<ConfigRelease>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = CreatePublishHandler(fixture);
        var result = await handler.Handle(
            new PublishConfigReleaseCommand(ProjectName, EnvironmentName, "1.1.0", MakeActive: true),
            CancellationToken.None);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Release!.Version).IsEqualTo("1.1.0");
        await Assert.That(result.Release.EntryCount).IsEqualTo(2);
        await Assert.That(result.Release.IsActive).IsTrue();
        await Assert.That(result.Release.Entries.Single(entry => entry.Key == "feature.enabled").Value).IsEqualTo("true");

        await fixture.ConfigReleaseRepository.Received(1).AddAsync(
            Arg.Is<ConfigRelease>(release =>
                release.Version == "1.1.0"
                && release.Major == 1
                && release.Minor == 1
                && release.Patch == 0
                && release.Entries.Count == 2
                && release.Actor == "alice"),
            Arg.Any<CancellationToken>());
        await fixture.EnvironmentRepository.Received(1).UpdateAsync(
            Arg.Is<ProjectEnvironment>(environment => environment.ActiveReleaseVersion == "1.1.0"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task PublishConfigRelease_RejectsDuplicateVersion()
    {
        var fixture = new TestFixture();
        fixture.SetupAsSystemAdmin();
        fixture.SetupProjectExists(ProjectName);
        fixture.SetupEnvironmentExists(ProjectName, EnvironmentName);
        fixture.ConfigReleaseRepository.ExistsAsync(ProjectName, EnvironmentName, "1.1.0", Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = CreatePublishHandler(fixture);
        var result = await handler.Handle(
            new PublishConfigReleaseCommand(ProjectName, EnvironmentName, "1.1.0", MakeActive: false),
            CancellationToken.None);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Release already exists");
        await fixture.ConfigReleaseRepository.DidNotReceive().AddAsync(
            Arg.Any<ConfigRelease>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SetActiveConfigRelease_UpdatesEnvironmentSelection()
    {
        var fixture = new TestFixture();
        fixture.SetupAsSystemAdmin();
        fixture.SetupProjectExists(ProjectName);
        fixture.SetupEnvironmentExists(ProjectName, EnvironmentName);
        fixture.ConfigReleaseRepository.ExistsAsync(ProjectName, EnvironmentName, "1.1.0", Arg.Any<CancellationToken>())
            .Returns(true);
        fixture.DateTime.NowUtc.Returns(new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc));

        var handler = new SetActiveConfigReleaseCommandHandler(
            fixture.ProjectRepository,
            fixture.EnvironmentRepository,
            fixture.ConfigReleaseRepository,
            fixture.ProjectAccessService,
            fixture.DateTime);

        var result = await handler.Handle(
            new SetActiveConfigReleaseCommand(ProjectName, EnvironmentName, "1.1.0"),
            CancellationToken.None);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Environment!.ActiveReleaseVersion).IsEqualTo("1.1.0");
        await fixture.EnvironmentRepository.Received(1).UpdateAsync(
            Arg.Is<ProjectEnvironment>(environment => environment.ActiveReleaseVersion == "1.1.0"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DeleteConfigRelease_RemovesNonActiveRelease()
    {
        var fixture = new TestFixture();
        fixture.SetupAsSystemAdmin();
        fixture.SetupProjectExists(ProjectName);
        fixture.SetupEnvironmentExists(ProjectName, EnvironmentName);
        fixture.ConfigReleaseRepository.DeleteAsync(
                ProjectName,
                EnvironmentName,
                "1.1.0",
                Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = new DeleteConfigReleaseCommandHandler(
            fixture.ProjectRepository,
            fixture.EnvironmentRepository,
            fixture.ConfigReleaseRepository,
            fixture.ProjectAccessService);
        var result = await handler.Handle(
            new DeleteConfigReleaseCommand(ProjectName, EnvironmentName, "1.1.0"),
            CancellationToken.None);

        await Assert.That(result.Success).IsTrue();
        await fixture.ConfigReleaseRepository.Received(1).DeleteAsync(
            ProjectName,
            EnvironmentName,
            "1.1.0",
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DeleteConfigRelease_RejectsActiveRelease()
    {
        var fixture = new TestFixture();
        fixture.SetupAsSystemAdmin();
        fixture.SetupProjectExists(ProjectName);
        fixture.EnvironmentRepository.GetAsync(ProjectName, EnvironmentName, Arg.Any<CancellationToken>())
            .Returns(new ProjectEnvironment
            {
                Project = ProjectName,
                Name = EnvironmentName,
                ActiveReleaseVersion = "1.1.0"
            });

        var handler = new DeleteConfigReleaseCommandHandler(
            fixture.ProjectRepository,
            fixture.EnvironmentRepository,
            fixture.ConfigReleaseRepository,
            fixture.ProjectAccessService);
        var result = await handler.Handle(
            new DeleteConfigReleaseCommand(ProjectName, EnvironmentName, "1.1.0"),
            CancellationToken.None);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Active release cannot be deleted");
        await fixture.ConfigReleaseRepository.DidNotReceive().DeleteAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task CreateConfigReleaseDraft_ReplacesWorkingConfigurationFromSnapshot()
    {
        var fixture = new TestFixture();
        fixture.SetupAsSystemAdmin("carol");
        fixture.SetupProjectExists(ProjectName);
        fixture.SetupEnvironmentExists(ProjectName, EnvironmentName);
        fixture.DateTime.NowUtc.Returns(new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc));
        fixture.ConfigReleaseRepository.GetAsync(ProjectName, EnvironmentName, "1.0.1", Arg.Any<CancellationToken>())
            .Returns(CreateRelease("1.0.1"));
        fixture.ConfigEntryRepository.ListAsync(ProjectName, EnvironmentName, Arg.Any<CancellationToken>())
            .Returns(
            [
                new ConfigEntry
                {
                    Project = ProjectName,
                    Environment = EnvironmentName,
                    Key = "feature.enabled",
                    Value = "false",
                    ContentType = "boolean",
                    Scope = KeyScope.Frontend,
                    CreatedAt = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new ConfigEntry
                {
                    Project = ProjectName,
                    Environment = EnvironmentName,
                    Key = "removed",
                    Value = "old",
                    ContentType = "text",
                    Scope = KeyScope.All
                }
            ]);

        var handler = new CreateConfigReleaseDraftCommandHandler(
            fixture.ProjectRepository,
            fixture.EnvironmentRepository,
            fixture.ConfigEntryRepository,
            fixture.ConfigReleaseRepository,
            fixture.ProjectAccessService,
            fixture.DateTime,
            fixture.CurrentUserService);

        var result = await handler.Handle(
            new CreateConfigReleaseDraftCommand(ProjectName, EnvironmentName, "1.0.1"),
            CancellationToken.None);

        await Assert.That(result.Success).IsTrue();
        await fixture.ConfigEntryRepository.Received(1).ReplaceEnvironmentAsync(
            ProjectName,
            EnvironmentName,
            Arg.Is<IReadOnlyList<ConfigEntry>>(entries =>
                entries.Count == 2
                && entries.Any(entry =>
                    entry.Key == "feature.enabled"
                    && entry.Value == "true"
                    && entry.ContentType == "boolean"
                    && entry.Scope == KeyScope.Frontend)
                && entries.Any(entry =>
                    entry.Key == "new.setting"
                    && entry.Value == "new")),
            Arg.Is<IReadOnlyList<string>>(keys => keys.SequenceEqual(new[] { "removed" })),
            "carol",
            Arg.Any<CancellationToken>());
        await fixture.ConfigEntryRepository.DidNotReceive().AddVersionAsync(
            Arg.Any<ConfigEntry>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
        await fixture.ConfigEntryRepository.DidNotReceive().DeleteManyAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<IEnumerable<string>>(),
            Arg.Any<CancellationToken>());
    }

    private static PublishConfigReleaseCommandHandler CreatePublishHandler(TestFixture fixture)
    {
        return new PublishConfigReleaseCommandHandler(
            fixture.ProjectRepository,
            fixture.EnvironmentRepository,
            fixture.ConfigEntryRepository,
            fixture.ConfigReleaseRepository,
            fixture.ProjectAccessService,
            fixture.DateTime,
            fixture.CurrentUserService);
    }

    private static ConfigRelease CreateRelease(string version)
    {
        return new ConfigRelease
        {
            Project = ProjectName,
            Environment = EnvironmentName,
            Version = version,
            Major = 1,
            Minor = 0,
            Patch = 1,
            Entries =
            [
                new ConfigReleaseEntry
                {
                    Project = ProjectName,
                    Environment = EnvironmentName,
                    ReleaseVersion = version,
                    Key = "feature.enabled",
                    Value = "true",
                    ContentType = "boolean",
                    Scope = KeyScope.Frontend
                },
                new ConfigReleaseEntry
                {
                    Project = ProjectName,
                    Environment = EnvironmentName,
                    ReleaseVersion = version,
                    Key = "new.setting",
                    Value = "new",
                    ContentType = "text",
                    Scope = KeyScope.All
                }
            ],
            EntryCount = 2
        };
    }
}
