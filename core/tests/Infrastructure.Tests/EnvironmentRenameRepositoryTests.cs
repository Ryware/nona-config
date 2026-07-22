using Microsoft.Data.Sqlite;
using Nona.Infrastructure.Repositories.Libsql;
using Nona.Infrastructure.Tests.Common;
using Nona.Libsql;

namespace Nona.Infrastructure.Tests;

[NotInParallel]
public class EnvironmentRenameRepositoryTests
{
    [Test]
    public async Task RenameEnvironmentAsync_UpdatesEnvironmentAndAllReferences()
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
            new LibsqlStatement($"INSERT INTO ApiKeys (Name, Key, Project, Environment, Scope, CreatedAt, UpdatedAt) VALUES ('client', 'secret', 'storefront', 'development', 1, '{timestamp}', '{timestamp}')")
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
