using Nona.Domain.Entities;
using Nona.Domain.Enums;
using Nona.Infrastructure.Repositories.Libsql;
using Nona.Libsql;

namespace Nona.Infrastructure.Tests;

public class LibsqlConfigEntryRepositoryTests
{
    [Test]
    public async Task AddVersionAsync_LocalLibsqlFile_AppendsHistoryAndServesLatestActiveValue()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"nona-config-entry-versions-{Guid.NewGuid():N}.db");

        try
        {
            using var client = new NelknetLibsqlDatabaseClient($"Data Source={databasePath}");
            var migrations = new LibsqlMigrationRunner(client, ResolveMigrationsFolder());
            await migrations.RunMigrationsAsync();

            var repository = new LibsqlConfigEntryRepository(client);
            var createdAt = new DateTime(2026, 6, 21, 10, 0, 0, DateTimeKind.Utc);
            var updatedAt = new DateTime(2026, 6, 22, 10, 0, 0, DateTimeKind.Utc);

            await repository.AddVersionAsync(new ConfigEntry
            {
                Project = "test-project",
                Environment = "production",
                Key = "feature.enabled",
                Value = "false",
                ContentType = "boolean",
                Scope = KeyScope.Frontend,
                CreatedAt = createdAt,
                UpdatedAt = createdAt
            }, "alice");

            var updated = await repository.AddVersionAsync(new ConfigEntry
            {
                Project = "test-project",
                Environment = "production",
                Key = "feature.enabled",
                Value = "true",
                ContentType = "boolean",
                Scope = KeyScope.Frontend,
                CreatedAt = createdAt,
                UpdatedAt = updatedAt
            }, "bob");

            var current = await repository.GetAsync("test-project", "production", "feature.enabled");
            var versions = await repository.ListVersionsAsync("test-project", "production", "feature.enabled");

            await Assert.That(updated).IsNotNull();
            await Assert.That(updated!.Value).IsEqualTo("true");
            await Assert.That(updated.ActiveVersion).IsEqualTo(2);
            await Assert.That(current).IsNotNull();
            await Assert.That(current!.Value).IsEqualTo("true");
            await Assert.That(current.ActiveVersion).IsEqualTo(2);
            await Assert.That(versions).Count().IsEqualTo(2);
            await Assert.That(versions[0].Version).IsEqualTo(2);
            await Assert.That(versions[0].Value).IsEqualTo("true");
            await Assert.That(versions[0].Actor).IsEqualTo("bob");
            await Assert.That(versions[1].Version).IsEqualTo(1);
            await Assert.That(versions[1].Value).IsEqualTo("false");
            await Assert.That(versions[1].Actor).IsEqualTo("alice");
        }
        finally
        {
            TryDelete(databasePath);
        }
    }

    [Test]
    public async Task AddVersionAsync_ReturnsWrittenProjectionFromWriteBatch()
    {
        var client = new StaleReadLibsqlClient();
        var repository = new LibsqlConfigEntryRepository(client);
        var createdAt = new DateTime(2026, 6, 21, 10, 0, 0, DateTimeKind.Utc);
        var updatedAt = new DateTime(2026, 6, 22, 10, 0, 0, DateTimeKind.Utc);

        var saved = await repository.AddVersionAsync(new ConfigEntry
        {
            Project = "test-project",
            Environment = "production",
            Key = "feature.enabled",
            Value = "true",
            ContentType = "boolean",
            Scope = KeyScope.Frontend,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        }, "alice");

        await Assert.That(saved).IsNotNull();
        await Assert.That(saved!.Value).IsEqualTo("true");
        await Assert.That(saved.ActiveVersion).IsEqualTo(2);
        await Assert.That(saved.UpdatedAt).IsEqualTo(updatedAt);
        await Assert.That(client.ExecuteAsyncCalls).IsEqualTo(0);
        await Assert.That(client.BatchStatementCount).IsEqualTo(3);
    }

    [Test]
    public async Task AddVersionAsync_FallsBackToSequentialStatements_WhenBatchReturnsNoProjection()
    {
        var client = new EmptyBatchLibsqlClient();
        var repository = new LibsqlConfigEntryRepository(client);
        var createdAt = new DateTime(2026, 6, 21, 10, 0, 0, DateTimeKind.Utc);
        var updatedAt = new DateTime(2026, 6, 22, 10, 0, 0, DateTimeKind.Utc);

        var saved = await repository.AddVersionAsync(new ConfigEntry
        {
            Project = "test-project",
            Environment = "production",
            Key = "feature.enabled",
            Value = "true",
            ContentType = "boolean",
            Scope = KeyScope.Frontend,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        }, "alice");

        await Assert.That(saved).IsNotNull();
        await Assert.That(saved!.Value).IsEqualTo("true");
        await Assert.That(saved.ActiveVersion).IsEqualTo(1);
        await Assert.That(client.BatchStatementCount).IsEqualTo(3);
        await Assert.That(client.ExecuteAsyncCalls).IsEqualTo(4);
    }

    [Test]
    public async Task AddVersionAsync_FallsBackToSequentialStatements_WhenBatchReturnsStaleProjection()
    {
        var client = new StaleBatchLibsqlClient();
        var repository = new LibsqlConfigEntryRepository(client);
        var createdAt = new DateTime(2026, 6, 21, 10, 0, 0, DateTimeKind.Utc);
        var updatedAt = new DateTime(2026, 6, 22, 10, 0, 0, DateTimeKind.Utc);

        var saved = await repository.AddVersionAsync(new ConfigEntry
        {
            Project = "test-project",
            Environment = "production",
            Key = "feature.enabled",
            Value = "true",
            ContentType = "boolean",
            Scope = KeyScope.Frontend,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        }, "alice");

        await Assert.That(saved).IsNotNull();
        await Assert.That(saved!.Value).IsEqualTo("true");
        await Assert.That(saved.ActiveVersion).IsEqualTo(2);
        await Assert.That(saved.UpdatedAt).IsEqualTo(updatedAt);
        await Assert.That(client.BatchStatementCount).IsEqualTo(3);
        await Assert.That(client.ExecuteAsyncCalls).IsEqualTo(4);
    }

    private sealed class StaleReadLibsqlClient : ILibsqlDatabaseClient
    {
        public int ExecuteAsyncCalls { get; private set; }
        public int BatchStatementCount { get; private set; }

        public Task<LibsqlQueryResult> ExecuteAsync(string sql, object? parameters = null, CancellationToken ct = default)
        {
            ExecuteAsyncCalls++;

            return Task.FromResult(new LibsqlQueryResult(
                [CreateConfigEntryRow("false", activeVersion: 1, updatedAt: "2026-06-21T10:00:00.0000000Z")],
                affectedRowCount: 0,
                lastInsertRowId: null));
        }

        public Task<IReadOnlyList<LibsqlQueryResult>> ExecuteBatchAsync(IEnumerable<LibsqlStatement> statements, CancellationToken ct = default)
        {
            BatchStatementCount = statements.Count();

            return Task.FromResult<IReadOnlyList<LibsqlQueryResult>>(
            [
                EmptyResult(),
                EmptyResult(),
                new LibsqlQueryResult(
                    [CreateConfigEntryRow("true", activeVersion: 2, updatedAt: "2026-06-22T10:00:00.0000000Z")],
                    affectedRowCount: 0,
                    lastInsertRowId: null)
            ]);
        }

        private static LibsqlQueryResult EmptyResult() => new([], affectedRowCount: 1, lastInsertRowId: null);

        private static LibsqlRow CreateConfigEntryRow(string value, int activeVersion, string updatedAt)
        {
            string[] columns =
            [
                "Project",
                "Environment",
                "Key",
                "Value",
                "ContentType",
                "Scope",
                "ActiveVersion",
                "CreatedAt",
                "UpdatedAt"
            ];

            return new LibsqlRow(columns, new Dictionary<string, object?>
            {
                ["Project"] = "test-project",
                ["Environment"] = "production",
                ["Key"] = "feature.enabled",
                ["Value"] = value,
                ["ContentType"] = "boolean",
                ["Scope"] = (int)KeyScope.Frontend,
                ["ActiveVersion"] = activeVersion,
                ["CreatedAt"] = "2026-06-21T10:00:00.0000000Z",
                ["UpdatedAt"] = updatedAt
            });
        }
    }

    private sealed class EmptyBatchLibsqlClient : ILibsqlDatabaseClient
    {
        public int ExecuteAsyncCalls { get; private set; }
        public int BatchStatementCount { get; private set; }

        public Task<LibsqlQueryResult> ExecuteAsync(string sql, object? parameters = null, CancellationToken ct = default)
        {
            ExecuteAsyncCalls++;

            if (sql.Contains("COALESCE(MAX(Version)", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new LibsqlQueryResult(
                    [new LibsqlRow(["Version"], new Dictionary<string, object?> { ["Version"] = 0 })],
                    affectedRowCount: 0,
                    lastInsertRowId: null));
            }

            if (sql.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new LibsqlQueryResult(
                    [CreateConfigEntryRow()],
                    affectedRowCount: 0,
                    lastInsertRowId: null));
            }

            return Task.FromResult(EmptyResult());
        }

        public Task<IReadOnlyList<LibsqlQueryResult>> ExecuteBatchAsync(IEnumerable<LibsqlStatement> statements, CancellationToken ct = default)
        {
            BatchStatementCount = statements.Count();

            return Task.FromResult<IReadOnlyList<LibsqlQueryResult>>(
            [
                EmptyResult(),
                EmptyResult(),
                EmptyResult()
            ]);
        }

        private static LibsqlQueryResult EmptyResult() => new([], affectedRowCount: 1, lastInsertRowId: null);

        private static LibsqlRow CreateConfigEntryRow()
        {
            string[] columns =
            [
                "Project",
                "Environment",
                "Key",
                "Value",
                "ContentType",
                "Scope",
                "ActiveVersion",
                "CreatedAt",
                "UpdatedAt"
            ];

            return new LibsqlRow(columns, new Dictionary<string, object?>
            {
                ["Project"] = "test-project",
                ["Environment"] = "production",
                ["Key"] = "feature.enabled",
                ["Value"] = "true",
                ["ContentType"] = "boolean",
                ["Scope"] = (int)KeyScope.Frontend,
                ["ActiveVersion"] = 1,
                ["CreatedAt"] = "2026-06-21T10:00:00.0000000Z",
                ["UpdatedAt"] = "2026-06-22T10:00:00.0000000Z"
            });
        }
    }

    private sealed class StaleBatchLibsqlClient : ILibsqlDatabaseClient
    {
        public int ExecuteAsyncCalls { get; private set; }
        public int BatchStatementCount { get; private set; }

        public Task<LibsqlQueryResult> ExecuteAsync(string sql, object? parameters = null, CancellationToken ct = default)
        {
            ExecuteAsyncCalls++;

            if (sql.Contains("COALESCE(MAX(Version)", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new LibsqlQueryResult(
                    [new LibsqlRow(["Version"], new Dictionary<string, object?> { ["Version"] = 1 })],
                    affectedRowCount: 0,
                    lastInsertRowId: null));
            }

            if (sql.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new LibsqlQueryResult(
                    [CreateConfigEntryRow("true", activeVersion: 2, updatedAt: "2026-06-22T10:00:00.0000000Z")],
                    affectedRowCount: 0,
                    lastInsertRowId: null));
            }

            return Task.FromResult(EmptyResult());
        }

        public Task<IReadOnlyList<LibsqlQueryResult>> ExecuteBatchAsync(IEnumerable<LibsqlStatement> statements, CancellationToken ct = default)
        {
            BatchStatementCount = statements.Count();

            return Task.FromResult<IReadOnlyList<LibsqlQueryResult>>(
            [
                EmptyResult(),
                EmptyResult(),
                new LibsqlQueryResult(
                    [CreateConfigEntryRow("false", activeVersion: 1, updatedAt: "2026-06-21T10:00:00.0000000Z")],
                    affectedRowCount: 0,
                    lastInsertRowId: null)
            ]);
        }

        private static LibsqlQueryResult EmptyResult() => new([], affectedRowCount: 1, lastInsertRowId: null);

        private static LibsqlRow CreateConfigEntryRow(string value, int activeVersion, string updatedAt)
        {
            string[] columns =
            [
                "Project",
                "Environment",
                "Key",
                "Value",
                "ContentType",
                "Scope",
                "ActiveVersion",
                "CreatedAt",
                "UpdatedAt"
            ];

            return new LibsqlRow(columns, new Dictionary<string, object?>
            {
                ["Project"] = "test-project",
                ["Environment"] = "production",
                ["Key"] = "feature.enabled",
                ["Value"] = value,
                ["ContentType"] = "boolean",
                ["Scope"] = (int)KeyScope.Frontend,
                ["ActiveVersion"] = activeVersion,
                ["CreatedAt"] = "2026-06-21T10:00:00.0000000Z",
                ["UpdatedAt"] = updatedAt
            });
        }
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

    private static void TryDelete(string databasePath)
    {
        try
        {
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
        catch
        {
        }
    }
}
