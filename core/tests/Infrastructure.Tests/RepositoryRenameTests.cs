using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nona.Domain.Entities;
using Nona.Domain.Enums;
using Nona.Infrastructure.Configuration;
using Nona.Infrastructure.Repositories.Libsql;
using Nona.Infrastructure.Tests.Common;
using Nona.Libsql;

namespace Nona.Infrastructure.Tests;

[NotInParallel]
public class RepositoryRenameTests
{
    [Test]
    public async Task InMemoryRepositories_RenameAllReferences()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:Type"] = "InMemory"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddStorageProvider(configuration);
        await using var provider = services.BuildServiceProvider();

        var projects = provider.GetRequiredService<Nona.Domain.Interfaces.IProjectRepository>();
        var environments = provider.GetRequiredService<Nona.Domain.Interfaces.IEnvironmentRepository>();
        var configEntries = provider.GetRequiredService<Nona.Domain.Interfaces.IConfigEntryRepository>();
        var releases = provider.GetRequiredService<Nona.Domain.Interfaces.IConfigReleaseRepository>();
        var apiKeys = provider.GetRequiredService<Nona.Domain.Interfaces.IApiKeyRepository>();
        var shareLinks = provider.GetRequiredService<Nona.Domain.Interfaces.IParameterShareLinkRepository>();
        var members = provider.GetRequiredService<Nona.Domain.Interfaces.IProjectMemberRepository>();
        var timestamp = new DateTime(2026, 7, 22, 10, 0, 0, DateTimeKind.Utc);

        await projects.AddAsync(new Project { Name = "storefront", UrlSlug = "storefront" });
        await environments.AddAsync(new ProjectEnvironment { Project = "storefront", Name = "development" });
        await configEntries.AddAsync(new ConfigEntry
        {
            Project = "storefront",
            Environment = "development",
            Key = "feature",
            Value = "true"
        });
        await releases.AddAsync(new ConfigRelease
        {
            Project = "storefront",
            Environment = "development",
            Version = "1.0.0",
            Entries =
            [
                new ConfigReleaseEntry
                {
                    Project = "storefront",
                    Environment = "development",
                    ReleaseVersion = "1.0.0",
                    Key = "feature",
                    Value = "true"
                }
            ]
        });
        await apiKeys.AddAsync(new ApiKey
        {
            Name = "client",
            Key = "secret",
            Project = "storefront",
            Environment = "development"
        });
        await shareLinks.AddAsync(new ParameterShareLink
        {
            TokenHash = "hash",
            Token = "token",
            Project = "storefront",
            Environment = "development",
            Key = "feature",
            CreatedBy = "tester"
        });
        await members.AddAsync(new ProjectMember
        {
            Username = "tester",
            ProjectId = "storefront",
            Role = ProjectRole.Editor
        });

        await projects.RenameAsync("storefront", "web-store", timestamp);
        await environments.RenameAsync("web-store", "development", "staging", timestamp);

        await Assert.That(await projects.GetByNameAsync("web-store")).IsNotNull();
        await Assert.That(await environments.GetAsync("web-store", "staging")).IsNotNull();
        await Assert.That(await configEntries.GetAsync("web-store", "staging", "feature")).IsNotNull();
        await Assert.That(await releases.GetAsync("web-store", "staging", "1.0.0")).IsNotNull();
        var releaseEntry = await releases.GetEntryAsync(
            "web-store",
            "staging",
            "1.0.0",
            "FEATURE",
            KeyScope.All);
        await Assert.That(releaseEntry.ReleaseFound).IsTrue();
        await Assert.That(releaseEntry.Entry!.Project).IsEqualTo("web-store");
        await Assert.That(releaseEntry.Entry.Environment).IsEqualTo("staging");
        await Assert.That((await apiKeys.ListByProjectAsync("web-store")).Single().Environment).IsEqualTo("staging");
        await Assert.That(await shareLinks.ListByConfigEntryAsync("web-store", "staging", "feature")).HasSingleItem();
        await Assert.That(await members.GetAsync("tester", "web-store")).IsNotNull();
    }

    [Test]
    public async Task ProjectRenameAsync_UpdatesProjectAndAllReferences()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"nona-rename-sqlite-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            using var client = new SqliteDatabaseClient(Path.Combine(directory, "nona.db"));
            var migrations = new LibsqlMigrationRunner(client, ResolveMigrationsFolder());
            await migrations.RunMigrationsAsync();
            await SeedEnvironmentReferencesAsync(client);

            var repository = new LibsqlProjectRepository(client);
            await repository.RenameAsync(
                "storefront",
                "web-store",
                new DateTime(2026, 7, 22, 12, 0, 0, DateTimeKind.Utc));

            var tableColumns = new Dictionary<string, string>
            {
                ["Projects"] = "Name",
                ["Environments"] = "Project",
                ["ConfigEntries"] = "Project",
                ["ConfigEntryVersions"] = "Project",
                ["ParameterShareLinks"] = "Project",
                ["ConfigReleases"] = "Project",
                ["ConfigReleaseEntries"] = "Project",
                ["ApiKeys"] = "Project",
                ["ProjectMembers"] = "ProjectName"
            };

            foreach (var (table, column) in tableColumns)
            {
                var renamed = await client.ExecuteAsync(
                    $"SELECT COUNT(1) AS Count FROM {table} WHERE {column} = @Name",
                    LibsqlParameters.Create(("Name", "web-store")));
                var old = await client.ExecuteAsync(
                    $"SELECT COUNT(1) AS Count FROM {table} WHERE {column} = @Name",
                    LibsqlParameters.Create(("Name", "storefront")));

                await Assert.That(renamed.Rows[0].GetInt32("Count")).IsEqualTo(1);
                await Assert.That(old.Rows[0].GetInt32("Count")).IsEqualTo(0);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Test]
    public async Task EnvironmentRenameAsync_UpdatesEnvironmentAndAllReferences()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"nona-rename-sqlite-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            using var client = new SqliteDatabaseClient(Path.Combine(directory, "nona.db"));
            var migrations = new LibsqlMigrationRunner(client, ResolveMigrationsFolder());
            await migrations.RunMigrationsAsync();
            await SeedEnvironmentReferencesAsync(client);

            var repository = new LibsqlEnvironmentRepository(client);
            await repository.RenameAsync(
                "storefront",
                "development",
                "staging",
                new DateTime(2026, 7, 22, 12, 0, 0, DateTimeKind.Utc));

            foreach (var table in new[]
                     {
                         "Environments",
                         "ConfigEntries",
                         "ConfigEntryVersions",
                         "ParameterShareLinks",
                         "ConfigReleases",
                         "ConfigReleaseEntries",
                         "ApiKeys"
                     })
            {
                var renamed = await client.ExecuteAsync(
                    $"SELECT COUNT(1) AS Count FROM {table} WHERE Project = @Project AND {(table == "Environments" ? "Name" : "Environment")} = @Environment",
                    LibsqlParameters.Create(("Project", "storefront"), ("Environment", "staging")));
                var old = await client.ExecuteAsync(
                    $"SELECT COUNT(1) AS Count FROM {table} WHERE Project = @Project AND {(table == "Environments" ? "Name" : "Environment")} = @Environment",
                    LibsqlParameters.Create(("Project", "storefront"), ("Environment", "development")));

                await Assert.That(renamed.Rows[0].GetInt32("Count")).IsEqualTo(1);
                await Assert.That(old.Rows[0].GetInt32("Count")).IsEqualTo(0);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static async Task SeedEnvironmentReferencesAsync(ILibsqlDatabaseClient client)
    {
        var timestamp = "2026-07-22T10:00:00.0000000Z";
        await client.ExecuteBatchAsync(
        [
            new LibsqlStatement($"INSERT INTO Projects (Name, UrlSlug, CreatedAt, UpdatedAt) VALUES ('storefront', 'storefront', '{timestamp}', '{timestamp}')"),
            new LibsqlStatement($"INSERT INTO Environments (Name, Project, ActiveReleaseVersion, CreatedAt, UpdatedAt) VALUES ('development', 'storefront', '1.0.0', '{timestamp}', '{timestamp}')"),
            new LibsqlStatement($"INSERT INTO ConfigEntries (Project, Environment, Key, Value, ContentType, Scope, ActiveVersion, CreatedAt, UpdatedAt) VALUES ('storefront', 'development', 'feature', 'true', 'boolean', 3, 1, '{timestamp}', '{timestamp}')"),
            new LibsqlStatement($"INSERT INTO ConfigEntryVersions (Project, Environment, Key, Version, Value, ContentType, Scope, CreatedAt, Actor) VALUES ('storefront', 'development', 'feature', 1, 'true', 'boolean', 3, '{timestamp}', 'tester')"),
            new LibsqlStatement($"INSERT INTO ParameterShareLinks (TokenHash, Token, Project, Environment, Key, CanEdit, CreatedBy, CreatedAt, ExpiresAt) VALUES ('hash', 'token', 'storefront', 'development', 'feature', 1, 'tester', '{timestamp}', '2027-07-22T10:00:00.0000000Z')"),
            new LibsqlStatement($"INSERT INTO ConfigReleases (Project, Environment, Version, Major, Minor, Patch, CreatedAt, Actor) VALUES ('storefront', 'development', '1.0.0', 1, 0, 0, '{timestamp}', 'tester')"),
            new LibsqlStatement("INSERT INTO ConfigReleaseEntries (Project, Environment, ReleaseVersion, Key, Value, ContentType, Scope) VALUES ('storefront', 'development', '1.0.0', 'feature', 'true', 'boolean', 3)"),
            new LibsqlStatement($"INSERT INTO ApiKeys (Name, Key, Project, Environment, Scope, CreatedAt, UpdatedAt) VALUES ('client', 'secret', 'storefront', 'development', 1, '{timestamp}', '{timestamp}')"),
            new LibsqlStatement($"INSERT INTO ProjectMembers (Username, ProjectName, Role, CreatedAt) VALUES ('tester', 'storefront', 1, '{timestamp}')")
        ]);
    }

    private static string ResolveMigrationsFolder()
    {
        var outputFolder = Path.Combine(AppContext.BaseDirectory, "Migrations");
        return Directory.Exists(outputFolder)
            ? outputFolder
            : Path.Combine(TestPaths.ResolveRepoRoot(), "core", "src", "Infrastructure", "Migrations");
    }
}
