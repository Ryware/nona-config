using Nona.Application.Api.ConfigEntries.Queries;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Entities;
using Nona.Domain.Enums;
using Nona.Domain.Interfaces;
using NSubstitute;

namespace Nona.Application.Tests.Api;

public class RuntimeSourceSeparationTests
{
    private const string Project = "project";
    private const string Environment = "production";
    private const string Key = "feature.enabled";
    private const string ApiKeyHash = "api-key-hash";

    [Test]
    public async Task WorkingSingleRead_IgnoresActiveRelease()
    {
        var fixture = new RuntimeFixture(activeReleaseVersion: "2.0.0");
        fixture.ConfigEntries.GetAsync(Project, Environment, Key, Arg.Any<CancellationToken>())
            .Returns(new ConfigEntry
            {
                Project = Project,
                Environment = Environment,
                Key = Key,
                Value = "working",
                ContentType = "text",
                Scope = KeyScope.Frontend
            });

        var result = await fixture.WorkingSingle().Handle(
            new GetConfigEntryValueQuery(Environment, Key),
            CancellationToken.None);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo("working");
        await fixture.Releases.DidNotReceiveWithAnyArgs().GetEntryAsync(
            default!, default!, default!, default!, default, default);
    }

    [Test]
    public async Task ReleaseSingleRead_WithoutActiveRelease_ReturnsStableConflictCode()
    {
        var fixture = new RuntimeFixture(activeReleaseVersion: null);

        var result = await fixture.ReleaseSingle().Handle(
            new GetReleaseConfigEntryValueQuery(Environment, Key),
            CancellationToken.None);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.ErrorCode).IsEqualTo(RuntimeConfigErrorCodes.ActiveReleaseNotConfigured);
        await fixture.ConfigEntries.DidNotReceiveWithAnyArgs().GetAsync(
            default!, default!, default!, default);
    }

    [Test]
    public async Task ReleaseSingleRead_WithExplicitSelector_DoesNotRequireActiveRelease()
    {
        var fixture = new RuntimeFixture(activeReleaseVersion: null);
        fixture.Releases.GetLatestPatchEntryAsync(
                Project,
                Environment,
                1,
                2,
                Key,
                KeyScope.Frontend,
                Arg.Any<CancellationToken>())
            .Returns(new ConfigReleaseEntryLookupResult(
                true,
                new ConfigReleaseEntry
                {
                    Project = Project,
                    Environment = Environment,
                    ReleaseVersion = "1.2.7",
                    Key = Key,
                    Value = "released",
                    ContentType = "text",
                    Scope = KeyScope.Frontend
                }));

        var result = await fixture.ReleaseSingle().Handle(
            new GetReleaseConfigEntryValueQuery(Environment, Key, "1.2.x"),
            CancellationToken.None);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo("released");
    }

    [Test]
    public async Task ReleaseSingleRead_MissingEntryAndMissingReleaseHaveDifferentCodes()
    {
        var fixture = new RuntimeFixture(activeReleaseVersion: "1.0.0");
        fixture.Releases.GetEntryAsync(
                Project,
                Environment,
                "1.0.0",
                Key,
                KeyScope.Frontend,
                Arg.Any<CancellationToken>())
            .Returns(
                new ConfigReleaseEntryLookupResult(false, null),
                new ConfigReleaseEntryLookupResult(true, null));

        var missingRelease = await fixture.ReleaseSingle().Handle(
            new GetReleaseConfigEntryValueQuery(Environment, Key),
            CancellationToken.None);
        var missingEntry = await fixture.ReleaseSingle().Handle(
            new GetReleaseConfigEntryValueQuery(Environment, Key),
            CancellationToken.None);

        await Assert.That(missingRelease.ErrorCode).IsEqualTo(RuntimeConfigErrorCodes.ReleaseNotFound);
        await Assert.That(missingEntry.ErrorCode).IsEqualTo(RuntimeConfigErrorCodes.ConfigEntryNotFound);
    }

    [Test]
    public async Task ReleaseBulkRead_UsesReleaseSourceAndSourceSpecificEtag()
    {
        var fixture = new RuntimeFixture(activeReleaseVersion: "1.0.0");
        fixture.Releases.GetMetadataAsync(Project, Environment, "1.0.0", Arg.Any<CancellationToken>())
            .Returns(new ConfigRelease
            {
                Project = Project,
                Environment = Environment,
                Version = "1.0.0",
                Major = 1,
                Minor = 0,
                Patch = 0,
                EntryCount = 1,
                CreatedAt = DateTime.UnixEpoch
            });
        fixture.Releases.ListEntriesAsync(
                Project,
                Environment,
                "1.0.0",
                KeyScope.Frontend,
                Arg.Any<CancellationToken>())
            .Returns([
                new ConfigReleaseEntry
                {
                    Project = Project,
                    Environment = Environment,
                    ReleaseVersion = "1.0.0",
                    Key = Key,
                    Value = "released",
                    ContentType = "text",
                    Scope = KeyScope.Frontend
                }
            ]);
        fixture.ConfigEntries.ListAsync(Project, Environment, Arg.Any<CancellationToken>())
            .Returns([
                new ConfigEntry
                {
                    Project = Project,
                    Environment = Environment,
                    Key = Key,
                    Value = "working",
                    ContentType = "text",
                    Scope = KeyScope.Frontend
                }
            ]);

        var working = await fixture.WorkingBulk().Handle(
            new GetAllConfigValuesQuery(Environment),
            CancellationToken.None);
        var released = await fixture.ReleaseBulk().Handle(
            new GetAllReleaseConfigValuesQuery(Environment),
            CancellationToken.None);

        await Assert.That(working.Values![Key].Value).IsEqualTo("working");
        await Assert.That(released.Values![Key].Value).IsEqualTo("released");
        await Assert.That(released.Etag).IsNotEqualTo(working.Etag);
    }

    private sealed class RuntimeFixture
    {
        public IApiKeyRepository ApiKeys { get; } = Substitute.For<IApiKeyRepository>();
        public IEnvironmentRepository Environments { get; } = Substitute.For<IEnvironmentRepository>();
        public IConfigEntryRepository ConfigEntries { get; } = Substitute.For<IConfigEntryRepository>();
        public IConfigReleaseRepository Releases { get; } = Substitute.For<IConfigReleaseRepository>();
        public IApiKeyService ApiKeyService { get; } = Substitute.For<IApiKeyService>();

        public RuntimeFixture(string? activeReleaseVersion)
        {
            ApiKeyService.GetCurrentApiKeyHash().Returns(ApiKeyHash);
            ApiKeys.GetByKeyHashAsync(ApiKeyHash, Arg.Any<CancellationToken>())
                .Returns(new ApiKeyAuthenticationResult(
                    new Project { Name = Project },
                    KeyScope.Frontend,
                    Environment));
            Environments.GetAsync(Project, Environment, Arg.Any<CancellationToken>())
                .Returns(new ProjectEnvironment
                {
                    Project = Project,
                    Name = Environment,
                    ActiveReleaseVersion = activeReleaseVersion
                });
        }

        public GetConfigEntryValueQueryHandler WorkingSingle() => new(
            ApiKeys,
            Environments,
            ConfigEntries,
            ApiKeyService);

        public GetReleaseConfigEntryValueQueryHandler ReleaseSingle() => new(
            ApiKeys,
            Environments,
            Releases,
            ApiKeyService);

        public GetAllConfigValuesQueryHandler WorkingBulk() => new(
            ApiKeys,
            Environments,
            ConfigEntries,
            ApiKeyService);

        public GetAllReleaseConfigValuesQueryHandler ReleaseBulk() => new(
            ApiKeys,
            Environments,
            Releases,
            ApiKeyService);
    }
}
