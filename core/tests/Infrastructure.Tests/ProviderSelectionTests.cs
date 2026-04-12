using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nona.Libsql;
using Nona.Domain.Interfaces;
using Nona.Infrastructure.Repositories.Libsql;
using Nona.Infrastructure.Repositories.Sqlite;

namespace Nona.Infrastructure.Tests;

public class ProviderSelectionTests
{
    [Test]
    public async Task ConfigureServices_SelectsSqliteRepositories_WhenConfigured()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:Type"] = "Sqlite",
                ["ConnectionStrings:Sqlite"] = "Data Source=:memory:"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddInfrastructureServices(configuration);

        using var provider = services.BuildServiceProvider();

        var configEntries = provider.GetRequiredService<IConfigEntryRepository>();
        var projects = provider.GetRequiredService<IProjectRepository>();

        await Assert.That(configEntries.GetType().FullName)
            .IsEqualTo(typeof(SqliteConfigEntryRepository).FullName);
        await Assert.That(projects.GetType().FullName)
            .IsEqualTo(typeof(SqliteProjectRepository).FullName);
    }

    [Test]
    public async Task ConfigureServices_SelectsLibsqlRepositories_WhenConfigured()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:Type"] = "Libsql",
                ["ConnectionStrings:Libsql"] = "libsql://integration.test",
                ["Storage:Libsql:AuthToken"] = "token",
                ["Storage:Libsql:TimeoutSeconds"] = "15"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddInfrastructureServices(configuration);

        using var provider = services.BuildServiceProvider();

        var configEntries = provider.GetRequiredService<IConfigEntryRepository>();
        var projects = provider.GetRequiredService<IProjectRepository>();

        await Assert.That(configEntries.GetType().FullName)
            .IsEqualTo(typeof(LibsqlConfigEntryRepository).FullName);
        await Assert.That(projects.GetType().FullName)
            .IsEqualTo(typeof(LibsqlProjectRepository).FullName);
    }

    [Test]
    public async Task ConfigureServices_SelectsMirroredLocalLibsqlClient_WhenConfigured()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:Type"] = "Libsql",
                ["ConnectionStrings:Libsql"] = "http://127.0.0.1:9999",
                ["Storage:Libsql:AuthToken"] = "token",
                ["Storage:Libsql:EnableLocalReplica"] = "true",
                ["Storage:Libsql:LocalReplicaPath"] = Path.Combine(Path.GetTempPath(), $"nona-provider-selection-{Guid.NewGuid():N}.db")
            })
            .Build();

        var services = new ServiceCollection();
        services.AddInfrastructureServices(configuration);

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<ILibsqlDatabaseClient>();

        await Assert.That(client.GetType().FullName)
            .IsEqualTo(typeof(LibsqlMirroredLocalDatabaseClient).FullName);
    }

    [Test]
    public async Task ConfigureServices_AllowsPrimaryLocalReplicaWithoutRemoteUrl_WhenConfigured()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:Type"] = "Libsql",
                ["Storage:Libsql:AuthToken"] = "token",
                ["Storage:Libsql:EnableLocalReplica"] = "true",
                ["Storage:Libsql:LocalReplicaPath"] = Path.Combine(Path.GetTempPath(), $"nona-primary-selection-{Guid.NewGuid():N}.db"),
                ["Storage:Libsql:LocalReplicaRole"] = "Primary"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddInfrastructureServices(configuration);

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<ILibsqlDatabaseClient>();

        await Assert.That(client.GetType().FullName)
            .IsEqualTo(typeof(LibsqlMirroredLocalDatabaseClient).FullName);
    }
}
