using Microsoft.Data.Sqlite;
using Nona.Domain.Entities;
using Nona.Domain.Enums;
using Nona.Infrastructure.Repositories.Libsql;
using Nona.Infrastructure.Tests.Common;
using Nona.Libsql;

namespace Nona.Infrastructure.Tests;

[NotInParallel]
public class AuditLogRepositoryTests
{
    [Test]
    public async Task Repository_RoundTripsPersistedActionKind()
    {
        var directory = CreateTempDirectory("nona-audit-log");

        try
        {
            using var client = new SqliteDatabaseClient(Path.Combine(directory, "nona.db"));
            var migrations = new LibsqlMigrationRunner(client, ResolveMigrationsFolder());
            await migrations.RunMigrationsAsync();

            var repository = new LibsqlAuditLogRepository(client);
            await repository.AddAsync(new AuditLogEntry
            {
                Actor = "audit.user@example.test",
                ActorIsSystem = false,
                ActionKind = AuditActionKind.Update,
                Action = "Set Active Config Release",
                Target = "1.3.1",
                Project = "sample-project",
                Environment = "production",
                CreatedAt = new DateTime(2026, 7, 29, 12, 0, 0, DateTimeKind.Utc)
            });

            var entry = (await repository.ListAsync()).Single();

            await Assert.That(entry.ActionKind).IsEqualTo(AuditActionKind.Update);
            await Assert.That(entry.Action).IsEqualTo("Set Active Config Release");
        }
        finally
        {
            DeleteTempDirectory(directory);
        }
    }

    [Test]
    public async Task Migration018_BackfillsHistoricalActionKinds()
    {
        var directory = CreateTempDirectory("nona-audit-migration");
        var migrationsDirectory = Path.Combine(directory, "Migrations");
        Directory.CreateDirectory(migrationsDirectory);

        try
        {
            foreach (var migration in Directory.GetFiles(ResolveMigrationsFolder(), "*.sql")
                         .Where(path => string.Compare(
                             Path.GetFileName(path),
                             "018_AddAuditLogActionKind.sql",
                             StringComparison.Ordinal) < 0))
            {
                File.Copy(migration, Path.Combine(migrationsDirectory, Path.GetFileName(migration)));
            }

            using var client = new SqliteDatabaseClient(Path.Combine(directory, "nona.db"));
            var migrations = new LibsqlMigrationRunner(client, migrationsDirectory);
            await migrations.RunMigrationsAsync();

            var historicalActions = new[]
            {
                "Created Project",
                "Published Config Release",
                "Set Active Config Release",
                "Cleared Active Config Release",
                "Rolled Back Key to v3",
                "Share Link Revoked",
                "Deleted Config Release",
                "Share Link Accessed",
                "Unrecognized Historical Event"
            };

            foreach (var action in historicalActions)
            {
                await client.ExecuteAsync(
                    """
                    INSERT INTO AuditLogs (Actor, ActorIsSystem, Action, Target, CreatedAt)
                    VALUES (@Actor, 0, @Action, 'target', '2026-07-29T12:00:00.0000000Z')
                    """,
                    LibsqlParameters.Create(
                        ("Actor", "audit.user@example.test"),
                        ("Action", action)));
            }

            var migration018 = Path.Combine(ResolveMigrationsFolder(), "018_AddAuditLogActionKind.sql");
            File.Copy(migration018, Path.Combine(migrationsDirectory, Path.GetFileName(migration018)));
            await migrations.RunMigrationsAsync();

            var result = await client.ExecuteAsync(
                "SELECT Action, ActionKind FROM AuditLogs ORDER BY rowid");
            var actual = result.Rows.ToDictionary(
                row => row.GetString("Action"),
                row => row.GetString("ActionKind"));

            await Assert.That(actual["Created Project"]).IsEqualTo("create");
            await Assert.That(actual["Published Config Release"]).IsEqualTo("create");
            await Assert.That(actual["Set Active Config Release"]).IsEqualTo("update");
            await Assert.That(actual["Cleared Active Config Release"]).IsEqualTo("update");
            await Assert.That(actual["Rolled Back Key to v3"]).IsEqualTo("update");
            await Assert.That(actual["Share Link Revoked"]).IsEqualTo("update");
            await Assert.That(actual["Deleted Config Release"]).IsEqualTo("delete");
            await Assert.That(actual["Share Link Accessed"]).IsEqualTo("activity");
            await Assert.That(actual["Unrecognized Historical Event"]).IsEqualTo("activity");
        }
        finally
        {
            DeleteTempDirectory(directory);
        }
    }

    private static string CreateTempDirectory(string prefix)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void DeleteTempDirectory(string directory)
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
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
