using Nona.Infrastructure.Repositories.Libsql;
using Nona.Libsql;

namespace Nona.Infrastructure.Tests;

public class LibsqlProjectRepositoryTests
{
    [Test]
    public async Task GetByNameAsync_QueriesProjectName()
    {
        var client = new ProjectLookupClient();
        var repository = new LibsqlProjectRepository(client);

        var project = await repository.GetByNameAsync("Display Name");

        await Assert.That(project).IsNotNull();
        await Assert.That(project!.Name).IsEqualTo("Display Name");
        await Assert.That(project.UrlSlug).IsEqualTo("display-name");
        await Assert.That(client.LastSql.Contains("WHERE Name = @Name", StringComparison.OrdinalIgnoreCase)).IsTrue();
        await Assert.That(client.LastParameters["Name"]).IsEqualTo("Display Name");
    }

    [Test]
    public async Task ExistsAsync_QueriesProjectName()
    {
        var client = new ProjectLookupClient();
        var repository = new LibsqlProjectRepository(client);

        var exists = await repository.ExistsAsync("Display Name");

        await Assert.That(exists).IsTrue();
        await Assert.That(client.LastSql.Contains("WHERE Name = @Name", StringComparison.OrdinalIgnoreCase)).IsTrue();
        await Assert.That(client.LastParameters["Name"]).IsEqualTo("Display Name");
    }

    private sealed class ProjectLookupClient : ILibsqlDatabaseClient
    {
        public string LastSql { get; private set; } = string.Empty;

        public IReadOnlyDictionary<string, object?> LastParameters { get; private set; } =
            new Dictionary<string, object?>();

        public Task<LibsqlQueryResult> ExecuteAsync(string sql, object? parameters = null, CancellationToken ct = default)
        {
            LastSql = sql;
            LastParameters = (IReadOnlyDictionary<string, object?>)(parameters ?? new Dictionary<string, object?>());

            if (sql.Contains("COUNT(1)", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new LibsqlQueryResult(
                    [new LibsqlRow(["Count"], new Dictionary<string, object?> { ["Count"] = 1 })],
                    affectedRowCount: 0,
                    lastInsertRowId: null));
            }

            return Task.FromResult(new LibsqlQueryResult(
                [new LibsqlRow(
                    ["Id", "Name", "UrlSlug", "CreatedAt", "UpdatedAt"],
                    new Dictionary<string, object?>
                    {
                        ["Id"] = 42L,
                        ["Name"] = "Display Name",
                        ["UrlSlug"] = "display-name",
                        ["CreatedAt"] = "2026-07-01T12:00:00.0000000Z",
                        ["UpdatedAt"] = "2026-07-02T12:00:00.0000000Z"
                    })],
                affectedRowCount: 0,
                lastInsertRowId: null));
        }

        public Task<IReadOnlyList<LibsqlQueryResult>> ExecuteBatchAsync(
            IEnumerable<LibsqlStatement> statements,
            CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }
    }
}
