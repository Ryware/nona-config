using Microsoft.Extensions.Options;
using Nona.Infrastructure.Services;
using Nona.Libsql;

namespace Nona.Infrastructure.Tests;

public class LibsqlDatabaseInitializerTests
{
    [Test]
    public async Task StartAsync_SkipsMigrations_WhenLocalReplicaEnabled()
    {
        var client = new ThrowingLibsqlDatabaseClient();
        var initializer = new LibsqlDatabaseInitializer(
            client,
            Options.Create(new LibsqlOptions
            {
                DataSource = "http://primary.test",
                EnableLocalReplica = true,
                LocalReplicaPath = "replica.db"
            }));

        await initializer.StartAsync(CancellationToken.None);
        await Assert.That(client.WasCalled).IsFalse();
    }

    private sealed class ThrowingLibsqlDatabaseClient : ILibsqlDatabaseClient
    {
        public bool WasCalled { get; private set; }

        public Task<LibsqlQueryResult> ExecuteAsync(string sql, object? parameters = null, CancellationToken ct = default)
        {
            WasCalled = true;
            throw new InvalidOperationException("Migrations should not run for embedded replicas.");
        }

        public Task<IReadOnlyList<LibsqlQueryResult>> ExecuteBatchAsync(
            IEnumerable<LibsqlStatement> statements,
            CancellationToken ct = default)
        {
            WasCalled = true;
            throw new InvalidOperationException("Migrations should not run for embedded replicas.");
        }
    }
}
