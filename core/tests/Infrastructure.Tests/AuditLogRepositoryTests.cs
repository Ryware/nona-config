using Microsoft.Data.Sqlite;
using Nona.Domain.Entities;
using Nona.Domain.Enums;
using Nona.Domain.Interfaces;
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

            var entry = (await repository.ListAsync(AllEntriesRequest())).Items.Single();

            await Assert.That(entry.ActionKind).IsEqualTo(AuditActionKind.Update);
            await Assert.That(entry.Action).IsEqualTo("Set Active Config Release");
        }
        finally
        {
            DeleteTempDirectory(directory);
        }
    }

    [Test]
    public async Task Repository_FiltersCountsAndPaginatesOnTheServer()
    {
        var directory = CreateTempDirectory("nona-audit-query");

        try
        {
            using var client = new SqliteDatabaseClient(Path.Combine(directory, "nona.db"));
            var migrations = new LibsqlMigrationRunner(client, ResolveMigrationsFolder());
            await migrations.RunMigrationsAsync();
            var repository = new LibsqlAuditLogRepository(client);

            for (var index = 0; index < 30; index++)
            {
                await repository.AddAsync(new AuditLogEntry
                {
                    Actor = index % 2 == 0 ? "matching.user@example.test" : "other.user@example.test",
                    ActorIsSystem = false,
                    ActionKind = AuditActionKind.Update,
                    Action = index < 28 ? "Updated Parameter" : "Deleted Parameter",
                    Target = $"target-{index:D2}",
                    Project = index < 28 ? "sample-project" : "other-project",
                    Environment = index < 28 ? "production" : "staging",
                    CreatedAt = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc).AddHours(index)
                });
            }

            var result = await repository.ListAsync(new AuditLogPageRequest(
                new AuditLogFilter(
                    Search: "matching.user",
                    Action: "updated parameter",
                    Environment: "PRODUCTION",
                    CreatedFrom: new DateTime(2026, 7, 1, 4, 0, 0, DateTimeKind.Utc),
                    CreatedToExclusive: new DateTime(2026, 7, 2, 0, 0, 0, DateTimeKind.Utc)),
                Offset: 2,
                Limit: 5));

            await Assert.That(result.TotalCount).IsEqualTo(10);
            await Assert.That(result.Items).Count().IsEqualTo(5);
            await Assert.That(result.Items[0].Target).IsEqualTo("target-18");
            await Assert.That(result.Items[4].Target).IsEqualTo("target-10");
            await Assert.That(result.Actions).IsEquivalentTo(["Deleted Parameter", "Updated Parameter"]);
            await Assert.That(result.Environments).IsEquivalentTo(["production", "staging"]);
        }
        finally
        {
            DeleteTempDirectory(directory);
        }
    }

    [Test]
    public async Task Repository_ListsLargeExportsInStableCursorBatches()
    {
        var directory = CreateTempDirectory("nona-audit-export-query");
        var databasePath = Path.Combine(directory, "nona.db");

        try
        {
            using var client = new SqliteDatabaseClient(databasePath);
            var migrations = new LibsqlMigrationRunner(client, ResolveMigrationsFolder());
            await migrations.RunMigrationsAsync();
            var capturingClient = new CapturingLibsqlDatabaseClient(client);
            var repository = new LibsqlAuditLogRepository(capturingClient);
            var createdAt = new DateTime(2026, 7, 29, 12, 0, 0, DateTimeKind.Utc);

            for (var index = 0; index < 505; index++)
            {
                await repository.AddAsync(new AuditLogEntry
                {
                    Actor = "matching.user@example.test",
                    ActorIsSystem = false,
                    ActionKind = AuditActionKind.Update,
                    Action = "Updated Parameter",
                    Target = $"target-{index}",
                    Project = "sample-project",
                    Environment = "production",
                    CreatedAt = createdAt
                });
            }

            var first = await repository.ListBatchAsync(new AuditLogBatchRequest(
                new AuditLogFilter(Search: "matching.user"),
                BeforeCreatedAt: null,
                BeforeId: null,
                Limit: 500));
            var last = first[^1];
            var second = await repository.ListBatchAsync(new AuditLogBatchRequest(
                new AuditLogFilter(Search: "matching.user"),
                last.CreatedAt,
                last.Id,
                Limit: 500));

            await Assert.That(first).Count().IsEqualTo(500);
            await Assert.That(second).Count().IsEqualTo(5);
            await Assert.That(first[0].Id).IsEqualTo(1);
            await Assert.That(first[^1].Id).IsEqualTo(500);
            await Assert.That(second[0].Id).IsEqualTo(501);
            await Assert.That(first.Select(entry => entry.Id).Intersect(second.Select(entry => entry.Id))).IsEmpty();
            await Assert.That(first.Concat(second).Select(entry => entry.Id).Distinct()).Count().IsEqualTo(505);

            var queryPlanDetails = await ExplainQueryPlanAsync(
                databasePath,
                capturingClient.LastSql,
                capturingClient.LastParameters);
            await Assert.That(queryPlanDetails).Contains("IX_AuditLogs_CreatedAt");
            await Assert.That(queryPlanDetails).DoesNotContain("USE TEMP B-TREE");
        }
        finally
        {
            DeleteTempDirectory(directory);
        }
    }

    [Test]
    public async Task Repository_FiltersGlobalScopeEntries()
    {
        var directory = CreateTempDirectory("nona-audit-global-scope");

        try
        {
            using var client = new SqliteDatabaseClient(Path.Combine(directory, "nona.db"));
            var migrations = new LibsqlMigrationRunner(client, ResolveMigrationsFolder());
            await migrations.RunMigrationsAsync();
            var repository = new LibsqlAuditLogRepository(client);
            var createdAt = new DateTime(2026, 7, 29, 12, 0, 0, DateTimeKind.Utc);

            foreach (var environment in new string?[] { null, " ", "production" })
            {
                await repository.AddAsync(new AuditLogEntry
                {
                    Actor = "audit.user@example.test",
                    ActionKind = AuditActionKind.Create,
                    Action = "Created Project",
                    Target = environment ?? "null-environment",
                    Environment = environment,
                    CreatedAt = createdAt
                });
            }

            var result = await repository.ListAsync(new AuditLogPageRequest(
                new AuditLogFilter(Environment: AuditLogFilter.GlobalScopeEnvironment),
                Offset: 0,
                Limit: 25));

            await Assert.That(result.TotalCount).IsEqualTo(2);
            await Assert.That(result.Items.All(entry => string.IsNullOrWhiteSpace(entry.Environment))).IsTrue();
            await Assert.That(result.Environments)
                .IsEquivalentTo([AuditLogFilter.GlobalScopeEnvironment, "production"]);
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

    private static AuditLogPageRequest AllEntriesRequest()
    {
        return new AuditLogPageRequest(new AuditLogFilter(), 0, int.MaxValue);
    }

    private static async Task<string> ExplainQueryPlanAsync(
        string databasePath,
        string sql,
        object? parameters)
    {
        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"EXPLAIN QUERY PLAN {sql}";

        foreach (var parameter in (IReadOnlyDictionary<string, object?>)parameters!)
        {
            command.Parameters.AddWithValue($"@{parameter.Key}", parameter.Value ?? DBNull.Value);
        }

        var details = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            details.Add(reader.GetString(reader.GetOrdinal("detail")));
        }

        return string.Join(" | ", details);
    }

    private sealed class CapturingLibsqlDatabaseClient(ILibsqlDatabaseClient inner) : ILibsqlDatabaseClient
    {
        public string LastSql { get; private set; } = string.Empty;
        public object? LastParameters { get; private set; }

        public async Task<LibsqlQueryResult> ExecuteAsync(
            string sql,
            object? parameters = null,
            CancellationToken ct = default)
        {
            LastSql = sql;
            LastParameters = parameters;
            return await inner.ExecuteAsync(sql, parameters, ct);
        }

        public Task<IReadOnlyList<LibsqlQueryResult>> ExecuteBatchAsync(
            IEnumerable<LibsqlStatement> statements,
            CancellationToken ct = default)
        {
            return inner.ExecuteBatchAsync(statements, ct);
        }
    }
}
