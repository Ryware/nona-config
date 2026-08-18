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
            await Assert.That(migrationCount.Rows[0].GetInt32("Count")).IsEqualTo(23);
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
    public async Task ExplicitAdminRoleMigration_BackfillsFirstUserWithoutDatabaseRoleLock()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"nona-admin-role-sqlite-{Guid.NewGuid():N}");
        var migrationsDirectory = Path.Combine(directory, "Migrations");
        Directory.CreateDirectory(migrationsDirectory);

        try
        {
            var sourceMigrations = ResolveMigrationsFolder();
            foreach (var migration in Directory.GetFiles(sourceMigrations, "*.sql")
                         .Where(path => !Path.GetFileName(path).StartsWith("020_", StringComparison.OrdinalIgnoreCase)
                             && !Path.GetFileName(path).StartsWith("021_", StringComparison.OrdinalIgnoreCase)))
            {
                File.Copy(migration, Path.Combine(migrationsDirectory, Path.GetFileName(migration)));
            }

            using var client = new SqliteDatabaseClient(Path.Combine(directory, "nona.db"));
            var migrations = new LibsqlMigrationRunner(client, migrationsDirectory);
            await migrations.RunMigrationsAsync();

            await client.ExecuteAsync(
                """
                INSERT INTO Users (Email, Name, Role, Scope, IsAdmin, CreatedAt, UpdatedAt)
                VALUES
                    ('admin@example.com', 'Admin', 0, 3, 1, '2026-08-04T00:00:00Z', '2026-08-04T00:00:00Z'),
                    ('editor@example.com', 'Editor', 1, 3, 0, '2026-08-04T00:01:00Z', '2026-08-04T00:01:00Z')
                """);

            File.Copy(
                Path.Combine(sourceMigrations, "020_MakeAdminRoleExplicit.sql"),
                Path.Combine(migrationsDirectory, "020_MakeAdminRoleExplicit.sql"));
            await migrations.RunMigrationsAsync();

            var users = await client.ExecuteAsync("SELECT Email, Role FROM Users ORDER BY rowid");
            var columns = await client.ExecuteAsync("PRAGMA table_info(Users)");

            await Assert.That(users.Rows[0].GetString("Email")).IsEqualTo("admin@example.com");
            await Assert.That(users.Rows[0].GetInt32("Role")).IsEqualTo((int)UserRole.Admin);
            await Assert.That(users.Rows[1].GetInt32("Role")).IsEqualTo(1);
            await Assert.That(columns.Rows.Select(row => row.GetString("name"))).DoesNotContain("IsAdmin");

            await client.ExecuteAsync("UPDATE Users SET Role = 1 WHERE Email = 'admin@example.com'");
            await client.ExecuteAsync("UPDATE Users SET Role = 2 WHERE Email = 'editor@example.com'");

            users = await client.ExecuteAsync("SELECT Email, Role FROM Users ORDER BY rowid");
            await Assert.That(users.Rows[0].GetInt32("Role")).IsEqualTo(1);
            await Assert.That(users.Rows[1].GetInt32("Role")).IsEqualTo((int)UserRole.Admin);
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
    public async Task MemberMigration_ConvertsRolesAndProtectsLastAdmin()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"nona-member-role-sqlite-{Guid.NewGuid():N}");
        var migrationsDirectory = Path.Combine(directory, "Migrations");
        Directory.CreateDirectory(migrationsDirectory);

        try
        {
            var sourceMigrations = ResolveMigrationsFolder();
            foreach (var migration in Directory.GetFiles(sourceMigrations, "*.sql")
                         .Where(path => !Path.GetFileName(path).StartsWith("021_", StringComparison.OrdinalIgnoreCase)))
            {
                File.Copy(migration, Path.Combine(migrationsDirectory, Path.GetFileName(migration)));
            }

            using var client = new SqliteDatabaseClient(Path.Combine(directory, "nona.db"));
            var migrations = new LibsqlMigrationRunner(client, migrationsDirectory);
            await migrations.RunMigrationsAsync();
            await client.ExecuteAsync(
                """
                INSERT INTO Users (Email, Name, Role, Scope, CreatedAt, UpdatedAt)
                VALUES
                    ('admin@example.com', 'Admin', 2, 3, '2026-08-09T00:00:00Z', '2026-08-09T00:00:00Z'),
                    ('viewer@example.com', 'Viewer', 0, 3, '2026-08-09T00:01:00Z', '2026-08-09T00:01:00Z'),
                    ('editor@example.com', 'Editor', 1, 3, '2026-08-09T00:02:00Z', '2026-08-09T00:02:00Z');

                INSERT INTO ProjectMembers (Username, ProjectName, Role, CreatedAt)
                VALUES
                    ('viewer@example.com', 'alpha', 0, '2026-08-09T00:03:00Z'),
                    ('editor@example.com', 'beta', 1, '2026-08-09T00:04:00Z');
                """);

            File.Copy(
                Path.Combine(sourceMigrations, "021_MigrateUsersToMembers.sql"),
                Path.Combine(migrationsDirectory, "021_MigrateUsersToMembers.sql"));
            await migrations.RunMigrationsAsync();

            var users = await client.ExecuteAsync("SELECT Email, Role FROM Users ORDER BY Email");
            var memberships = await client.ExecuteAsync("SELECT Username FROM ProjectMembers ORDER BY Username");

            await Assert.That(users.Rows.Single(row => row.GetString("Email") == "admin@example.com").GetInt32("Role"))
                .IsEqualTo((int)UserRole.Admin);
            await Assert.That(users.Rows.Where(row => row.GetString("Email") != "admin@example.com")
                .Select(row => row.GetInt32("Role"))).IsEquivalentTo([(int)UserRole.Member, (int)UserRole.Member]);
            await Assert.That(memberships.Rows.Select(row => row.GetString("Username")))
                .IsEquivalentTo(["viewer@example.com"]);

            await Assert.That(await LastAdminWriteIsBlockedAsync(
                client,
                "UPDATE Users SET Role = 0 WHERE Email = 'admin@example.com'"))
                .IsTrue();
            await Assert.That(await LastAdminWriteIsBlockedAsync(
                client,
                "DELETE FROM Users WHERE Email = 'admin@example.com'"))
                .IsTrue();

            await client.ExecuteAsync(
                """
                INSERT INTO Users (Email, Name, Role, Scope, CreatedAt, UpdatedAt)
                VALUES ('backup@example.com', 'Backup', 2, 3, '2026-08-09T00:05:00Z', '2026-08-09T00:05:00Z')
                """);
            await client.ExecuteAsync("UPDATE Users SET Role = 0 WHERE Email = 'admin@example.com'");
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
    public async Task ProjectMemberRepository_ListByUsersReturnsRequestedMemberships()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"nona-project-members-sqlite-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            using var client = new SqliteDatabaseClient(Path.Combine(directory, "nona.db"));
            var migrations = new LibsqlMigrationRunner(client, ResolveMigrationsFolder());
            await migrations.RunMigrationsAsync();

            var repository = new LibsqlProjectMemberRepository(client);
            await repository.AddAsync(new ProjectMember
            {
                Username = "first@example.com",
                ProjectId = "alpha",
                Role = ProjectRole.Editor
            });
            await repository.AddAsync(new ProjectMember
            {
                Username = "second@example.com",
                ProjectId = "beta",
                Role = ProjectRole.Viewer
            });
            await repository.AddAsync(new ProjectMember
            {
                Username = "other@example.com",
                ProjectId = "gamma",
                Role = ProjectRole.Viewer
            });

            var members = await repository.ListByUsersAsync(
                ["FIRST@example.com", "second@example.com", "first@example.com"]);

            await Assert.That(members.Count).IsEqualTo(2);
            await Assert.That(members.Select(member => member.Username))
                .IsEquivalentTo(["first@example.com", "second@example.com"]);
            await Assert.That(members.Select(member => member.ProjectId))
                .IsEquivalentTo(["alpha", "beta"]);
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

    private static async Task<bool> LastAdminWriteIsBlockedAsync(SqliteDatabaseClient client, string sql)
    {
        try
        {
            await client.ExecuteAsync(sql);
            return false;
        }
        catch (LibsqlException exception)
        {
            return exception.InnerException?.Message.Contains("At least one admin is required", StringComparison.Ordinal) == true;
        }
    }

}
