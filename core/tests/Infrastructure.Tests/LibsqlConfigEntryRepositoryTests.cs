using Nona.Domain.Entities;
using Nona.Domain.Enums;
using Nona.Infrastructure.Repositories.Libsql;
using Nona.Libsql;

namespace Nona.Infrastructure.Tests;

public class LibsqlConfigEntryRepositoryTests
{
    [Test]
    public async Task AddVersionAsync_ReturnsWrittenProjectionFromWriteBatch_WhenReadReplicaIsStale()
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
}
