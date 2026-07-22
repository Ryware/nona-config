using Nona.Domain.Entities;
using Nona.Domain.Enums;
using Nona.Infrastructure.Repositories.Libsql;
using Nona.Infrastructure.Tests.Common;
using Nona.Libsql;

namespace Nona.Infrastructure.Tests;

public class LibsqlConfigReleaseRepositoryTests
{
    [Test]
    public async Task AddAsync_Sqld_StoresReleaseEntriesAndResolvesHighestPatch()
    {
        await using var server = await LocalSqldTestServer.StartAsync();
        using var client = server.CreateClient();
        var migrations = new LibsqlMigrationRunner(client, ResolveMigrationsFolder());
        await migrations.RunMigrationsAsync();

        var repository = new LibsqlConfigReleaseRepository(client);

        await Assert.That(await repository.AddAsync(CreateRelease("1.1.0", "false", patch: 0))).IsTrue();
        await Assert.That(await repository.AddAsync(CreateRelease("1.1.2", "true", patch: 2))).IsTrue();
        await Assert.That(await repository.AddAsync(CreateRelease("1.1.2", "true", patch: 2))).IsFalse();

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
    public async Task AddAsync_Sqld_ConcurrentDuplicatePublishesReturnSingleSuccess()
    {
        await using var server = await LocalSqldTestServer.StartAsync();
        using var client = server.CreateClient();
        var migrations = new LibsqlMigrationRunner(client, ResolveMigrationsFolder());
        await migrations.RunMigrationsAsync();

        var repository = new LibsqlConfigReleaseRepository(client);
        var release = CreateRelease("1.1.0", "true", patch: 0);
        var results = await Task.WhenAll(
            Enumerable.Range(0, 8).Select(_ => repository.AddAsync(release)));

        var stored = await repository.ListAsync("test-project", "production");

        await Assert.That(results.Count(result => result)).IsEqualTo(1);
        await Assert.That(stored).Count().IsEqualTo(1);
        await Assert.That(stored[0].EntryCount).IsEqualTo(1);
    }

    [Test]
    public async Task ListEntriesAsync_Sqld_ReturnsOnlyEntriesMatchingRequiredScope()
    {
        await using var server = await LocalSqldTestServer.StartAsync();
        using var client = server.CreateClient();
        var migrations = new LibsqlMigrationRunner(client, ResolveMigrationsFolder());
        await migrations.RunMigrationsAsync();

        var repository = new LibsqlConfigReleaseRepository(client);
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
    public async Task GetEntryAsync_Sqld_ResolvesVersionAndScopeInSingleRoundTrip()
    {
        await using var server = await LocalSqldTestServer.StartAsync();
        using var client = server.CreateClient();
        var migrations = new LibsqlMigrationRunner(client, ResolveMigrationsFolder());
        await migrations.RunMigrationsAsync();

        var repository = new LibsqlConfigReleaseRepository(client);
        await repository.AddAsync(CreateRelease("1.1.0", "false", patch: 0));
        await repository.AddAsync(CreateRelease("1.1.2", "true", patch: 2));

        var countingClient = new CountingLibsqlDatabaseClient(client);
        var countingRepository = new LibsqlConfigReleaseRepository(countingClient);

        var exact = await countingRepository.GetEntryAsync(
            "TEST-PROJECT",
            "PRODUCTION",
            "1.1.0",
            "FEATURE.ENABLED",
            KeyScope.Frontend);

        await Assert.That(countingClient.RoundTrips).IsEqualTo(1);
        await Assert.That(exact.ReleaseFound).IsTrue();
        await Assert.That(exact.Entry).IsNotNull();
        await Assert.That(exact.Entry!.Value).IsEqualTo("false");

        countingClient.Reset();
        var latest = await countingRepository.GetLatestPatchEntryAsync(
            "test-project",
            "production",
            1,
            1,
            "feature.enabled",
            KeyScope.Frontend);

        await Assert.That(countingClient.RoundTrips).IsEqualTo(1);
        await Assert.That(latest.ReleaseFound).IsTrue();
        await Assert.That(latest.Entry).IsNotNull();
        await Assert.That(latest.Entry!.ReleaseVersion).IsEqualTo("1.1.2");
        await Assert.That(latest.Entry.Value).IsEqualTo("true");

        countingClient.Reset();
        var scopeDenied = await countingRepository.GetEntryAsync(
            "test-project",
            "production",
            "1.1.0",
            "feature.enabled",
            KeyScope.Backend);

        await Assert.That(countingClient.RoundTrips).IsEqualTo(1);
        await Assert.That(scopeDenied.ReleaseFound).IsTrue();
        await Assert.That(scopeDenied.Entry).IsNull();

        countingClient.Reset();
        var missingRelease = await countingRepository.GetEntryAsync(
            "test-project",
            "production",
            "9.9.9",
            "feature.enabled",
            KeyScope.Frontend);

        await Assert.That(countingClient.RoundTrips).IsEqualTo(1);
        await Assert.That(missingRelease.ReleaseFound).IsFalse();
        await Assert.That(missingRelease.Entry).IsNull();
    }

    [Test]
    public async Task DeleteAsync_Sqld_RemovesReleaseAndSnapshotEntries()
    {
        await using var server = await LocalSqldTestServer.StartAsync();
        using var client = server.CreateClient();
        var migrations = new LibsqlMigrationRunner(client, ResolveMigrationsFolder());
        await migrations.RunMigrationsAsync();

        var repository = new LibsqlConfigReleaseRepository(client);
        await repository.AddAsync(CreateRelease("1.1.0", "false", patch: 0));
        await repository.AddAsync(CreateRelease("1.1.2", "true", patch: 2));

        await Assert.That(await repository.DeleteAsync("test-project", "production", "1.1.0")).IsTrue();
        await Assert.That(await repository.DeleteAsync("test-project", "production", "1.1.0")).IsFalse();

        var releases = await repository.ListAsync("test-project", "production");
        var deletedEntries = await client.ExecuteAsync(
            """
            SELECT COUNT(1)
            FROM ConfigReleaseEntries
            WHERE Project = 'test-project'
              AND Environment = 'production'
              AND ReleaseVersion = '1.1.0'
            """);

        await Assert.That(releases).Count().IsEqualTo(1);
        await Assert.That(releases[0].Version).IsEqualTo("1.1.2");
        await Assert.That(deletedEntries.Rows[0].GetInt32(0)).IsEqualTo(0);
    }

    [Test]
    public async Task Migration016_Sqld_BackfillsExistingEnvironmentWithActiveRelease()
    {
        var sourceFolder = ResolveMigrationsFolder();
        var migrationsFolder = Path.Combine(Path.GetTempPath(), $"nona-release-migrations-{Guid.NewGuid():N}");
        Directory.CreateDirectory(migrationsFolder);

        try
        {
            foreach (var migrationFile in Directory.GetFiles(sourceFolder, "*.sql")
                         .Where(file => string.Compare(
                             Path.GetFileName(file),
                             "016_CreateConfigReleases.sql",
                             StringComparison.OrdinalIgnoreCase) < 0))
            {
                File.Copy(migrationFile, Path.Combine(migrationsFolder, Path.GetFileName(migrationFile)));
            }

            await using var server = await LocalSqldTestServer.StartAsync();
            using var client = server.CreateClient();
            var migrations = new LibsqlMigrationRunner(client, migrationsFolder);
            await migrations.RunMigrationsAsync();

            await client.ExecuteBatchAsync(
            [
                new LibsqlStatement(
                    """
                    INSERT INTO Environments (Name, Project, CreatedAt, UpdatedAt)
                    VALUES ('production', 'test-project', '2026-06-01T00:00:00.0000000Z', '2026-07-01T00:00:00.0000000Z')
                    """),
                new LibsqlStatement(
                    """
                    INSERT INTO ConfigEntries (
                        Project, Environment, Key, Value, ContentType, Scope, ActiveVersion, CreatedAt, UpdatedAt
                    )
                    VALUES (
                        'test-project', 'production', 'feature.enabled', 'true', 'boolean', 2, 1,
                        '2026-06-01T00:00:00.0000000Z', '2026-07-01T00:00:00.0000000Z'
                    )
                    """)
            ]);

            File.Copy(
                Path.Combine(sourceFolder, "016_CreateConfigReleases.sql"),
                Path.Combine(migrationsFolder, "016_CreateConfigReleases.sql"));
            await migrations.RunMigrationsAsync();

            var environment = await client.ExecuteAsync(
                "SELECT ActiveReleaseVersion FROM Environments WHERE Project = 'test-project' AND Name = 'production'");
            var repository = new LibsqlConfigReleaseRepository(client);
            var release = await repository.GetAsync("test-project", "production", "0.0.0");

            await Assert.That(environment.Rows[0].GetString("ActiveReleaseVersion")).IsEqualTo("0.0.0");
            await Assert.That(release).IsNotNull();
            await Assert.That(release!.Actor).IsEqualTo("Migration");
            await Assert.That(release.Entries).Count().IsEqualTo(1);
            await Assert.That(release.Entries[0].Key).IsEqualTo("feature.enabled");
            await Assert.That(release.Entries[0].Value).IsEqualTo("true");
        }
        finally
        {
            Directory.Delete(migrationsFolder, recursive: true);
        }
    }

    private static ConfigRelease CreateRelease(string version, string value, int patch)
    {
        return new ConfigRelease
        {
            Project = "test-project",
            Environment = "production",
            Version = version,
            Major = 1,
            Minor = 1,
            Patch = patch,
            Entries =
            [
                new ConfigReleaseEntry
                {
                    Project = "test-project",
                    Environment = "production",
                    ReleaseVersion = version,
                    Key = "feature.enabled",
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

    private static string ResolveMigrationsFolder()
    {
        var outputFolder = Path.Combine(AppContext.BaseDirectory, "Migrations");
        if (Directory.Exists(outputFolder))
        {
            return outputFolder;
        }

        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "..",
            "core",
            "src",
            "Infrastructure",
            "Migrations"));
    }

    private sealed class CountingLibsqlDatabaseClient(ILibsqlDatabaseClient inner) : ILibsqlDatabaseClient
    {
        public int RoundTrips { get; private set; }

        public async Task<LibsqlQueryResult> ExecuteAsync(
            string sql,
            object? parameters = null,
            CancellationToken ct = default)
        {
            RoundTrips++;
            return await inner.ExecuteAsync(sql, parameters, ct);
        }

        public async Task<IReadOnlyList<LibsqlQueryResult>> ExecuteBatchAsync(
            IEnumerable<LibsqlStatement> statements,
            CancellationToken ct = default)
        {
            RoundTrips++;
            return await inner.ExecuteBatchAsync(statements, ct);
        }

        public void Reset()
        {
            RoundTrips = 0;
        }
    }
}
