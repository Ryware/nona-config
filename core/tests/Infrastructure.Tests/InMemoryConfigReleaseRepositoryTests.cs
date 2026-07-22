using Nona.Domain.Entities;
using Nona.Domain.Enums;
using Nona.Infrastructure.Repositories.InMemory;

namespace Nona.Infrastructure.Tests;

public class InMemoryConfigReleaseRepositoryTests
{
    [Test]
    public async Task AddAsync_RejectsInvalidEntryKey()
    {
        var repository = new InMemoryConfigReleaseRepository();
        Exception? exception = null;
        try
        {
            await repository.AddAsync(CreateRelease("1.0.0", "true", patch: 0, key: "feature/value"));
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        await Assert.That(exception).IsTypeOf<ArgumentException>();
        await Assert.That(await repository.ListAsync("test-project", "production")).IsEmpty();
    }

    [Test]
    public async Task AddAsync_StoresImmutableReleaseAndResolvesHighestPatch()
    {
        var repository = new InMemoryConfigReleaseRepository();

        var first = CreateRelease("1.1.0", "false", patch: 0);
        var second = CreateRelease("1.1.2", "true", patch: 2);

        await Assert.That(await repository.AddAsync(first)).IsTrue();
        await Assert.That(await repository.AddAsync(second)).IsTrue();
        await Assert.That(await repository.AddAsync(second)).IsFalse();

        var exactMetadata = await repository.GetMetadataAsync("test-project", "production", "1.1.0");
        var latestMetadata = await repository.GetLatestPatchMetadataAsync("test-project", "production", 1, 1);
        var exact = await repository.GetAsync("test-project", "production", "1.1.0");
        var latest = await repository.GetLatestPatchAsync("test-project", "production", 1, 1);
        var releases = await repository.ListAsync("test-project", "production");

        await Assert.That(exactMetadata).IsNotNull();
        await Assert.That(exactMetadata!.Version).IsEqualTo("1.1.0");
        await Assert.That(exactMetadata.EntryCount).IsEqualTo(1);
        await Assert.That(exactMetadata.Entries).IsEmpty();
        await Assert.That(latestMetadata).IsNotNull();
        await Assert.That(latestMetadata!.Version).IsEqualTo("1.1.2");
        await Assert.That(latestMetadata.EntryCount).IsEqualTo(1);
        await Assert.That(latestMetadata.Entries).IsEmpty();
        await Assert.That(exact).IsNotNull();
        await Assert.That(exact!.Entries[0].Value).IsEqualTo("false");
        await Assert.That(latest).IsNotNull();
        await Assert.That(latest!.Version).IsEqualTo("1.1.2");
        await Assert.That(latest.Entries[0].Value).IsEqualTo("true");
        await Assert.That(releases).Count().IsEqualTo(2);
        await Assert.That(releases[0].Version).IsEqualTo("1.1.2");
        await Assert.That(releases[1].Version).IsEqualTo("1.1.0");
    }

    [Test]
    public async Task DeleteAsync_RemovesOnlyRequestedRelease()
    {
        var repository = new InMemoryConfigReleaseRepository();
        await repository.AddAsync(CreateRelease("1.1.0", "false", patch: 0));
        await repository.AddAsync(CreateRelease("1.1.2", "true", patch: 2));

        await Assert.That(await repository.DeleteAsync("test-project", "production", "1.1.0")).IsTrue();
        await Assert.That(await repository.DeleteAsync("test-project", "production", "1.1.0")).IsFalse();

        var releases = await repository.ListAsync("test-project", "production");
        await Assert.That(releases).Count().IsEqualTo(1);
        await Assert.That(releases[0].Version).IsEqualTo("1.1.2");
    }

    [Test]
    public async Task ListEntriesAsync_ReturnsOnlyEntriesMatchingRequiredScope()
    {
        var repository = new InMemoryConfigReleaseRepository();
        var release = CreateRelease("1.1.0", "false", patch: 0);
        release = new ConfigRelease
        {
            Project = release.Project,
            Environment = release.Environment,
            Version = release.Version,
            Major = release.Major,
            Minor = release.Minor,
            Patch = release.Patch,
            Entries =
            [
                release.Entries[0],
                new ConfigReleaseEntry
                {
                    Project = release.Project,
                    Environment = release.Environment,
                    ReleaseVersion = release.Version,
                    Key = "server.secret",
                    Value = "secret",
                    Scope = KeyScope.Backend
                }
            ],
            EntryCount = 2
        };
        await repository.AddAsync(release);

        var entries = await repository.ListEntriesAsync(
            "test-project",
            "production",
            "1.1.0",
            KeyScope.Frontend);

        await Assert.That(entries).Count().IsEqualTo(1);
        await Assert.That(entries[0].Key).IsEqualTo("feature.enabled");
    }

    [Test]
    public async Task GetEntryAsync_ResolvesExactAndLatestEntriesByKeyAndScope()
    {
        var repository = new InMemoryConfigReleaseRepository();
        await repository.AddAsync(CreateRelease("1.1.0", "false", patch: 0));
        await repository.AddAsync(CreateRelease("1.1.2", "true", patch: 2));

        var exact = await repository.GetEntryAsync(
            "TEST-PROJECT",
            "PRODUCTION",
            "1.1.0",
            "FEATURE.ENABLED",
            KeyScope.Frontend);
        var latest = await repository.GetLatestPatchEntryAsync(
            "test-project",
            "production",
            1,
            1,
            "feature.enabled",
            KeyScope.Frontend);
        var scopeDenied = await repository.GetEntryAsync(
            "test-project",
            "production",
            "1.1.0",
            "feature.enabled",
            KeyScope.Backend);
        var missingRelease = await repository.GetEntryAsync(
            "test-project",
            "production",
            "9.9.9",
            "feature.enabled",
            KeyScope.Frontend);

        await Assert.That(exact.ReleaseFound).IsTrue();
        await Assert.That(exact.Entry!.Value).IsEqualTo("false");
        await Assert.That(latest.ReleaseFound).IsTrue();
        await Assert.That(latest.Entry!.ReleaseVersion).IsEqualTo("1.1.2");
        await Assert.That(latest.Entry.Value).IsEqualTo("true");
        await Assert.That(scopeDenied.ReleaseFound).IsTrue();
        await Assert.That(scopeDenied.Entry).IsNull();
        await Assert.That(missingRelease.ReleaseFound).IsFalse();
        await Assert.That(missingRelease.Entry).IsNull();
    }

    private static ConfigRelease CreateRelease(
        string version,
        string value,
        int patch,
        string key = "feature.enabled")
    {
        var versionParts = version.Split('.');
        return new ConfigRelease
        {
            Project = "test-project",
            Environment = "production",
            Version = version,
            Major = int.Parse(versionParts[0]),
            Minor = int.Parse(versionParts[1]),
            Patch = patch,
            Entries =
            [
                new ConfigReleaseEntry
                {
                    Project = "test-project",
                    Environment = "production",
                    ReleaseVersion = version,
                    Key = key,
                    Value = value,
                    ContentType = "boolean",
                    Scope = KeyScope.Frontend
                }
            ],
            EntryCount = 1,
            CreatedAt = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc),
            Actor = "alice"
        };
    }

}
