using Microsoft.Data.Sqlite;
using Nona.Domain.Entities;
using Nona.Infrastructure.Repositories.Libsql;
using Nona.Infrastructure.Tests.Common;
using Nona.Libsql;

namespace Nona.Infrastructure.Tests;

[NotInParallel]
public class SqliteRepositoryCompatibilityTests
{
    [Test]
    public async Task ExistingMigrationsAndProjectRepository_WorkAgainstSqlite()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"nona-repository-sqlite-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            using var client = new SqliteDatabaseClient(Path.Combine(directory, "nona.db"));
            var migrations = new LibsqlMigrationRunner(client, ResolveMigrationsFolder());

            await migrations.RunMigrationsAsync();
            await migrations.RunMigrationsAsync();

            var repository = new LibsqlProjectRepository(client);
            var project = new Project
            {
                Name = "SQLite Project",
                UrlSlug = "sqlite-project",
                CreatedAt = new DateTime(2026, 7, 19, 12, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 7, 19, 12, 0, 0, DateTimeKind.Utc)
            };

            await repository.AddAsync(project);
            var loaded = await repository.GetByNameAsync("sqlite project");
            var migrationCount = await client.ExecuteAsync(
                "SELECT COUNT(1) AS Count FROM __MigrationsHistory");

            await Assert.That(project.Id).IsGreaterThan(0);
            await Assert.That(loaded).IsNotNull();
            await Assert.That(loaded!.UrlSlug).IsEqualTo("sqlite-project");
            await Assert.That(await repository.CountAsync()).IsEqualTo(1);
            await Assert.That(migrationCount.Rows[0].GetInt32("Count")).IsEqualTo(16);
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

    private static string ResolveMigrationsFolder()
    {
        var outputFolder = Path.Combine(AppContext.BaseDirectory, "Migrations");
        if (Directory.Exists(outputFolder))
        {
            return outputFolder;
        }

        return Path.Combine(
            TestPaths.ResolveRepoRoot(),
            "core",
            "src",
            "Infrastructure",
            "Migrations");
    }
}
