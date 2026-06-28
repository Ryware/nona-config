using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Nona.Domain.Interfaces;
using Nona.Infrastructure.Repositories.Libsql;
using Nona.Libsql;

namespace Nona.Infrastructure.Tests;

public class ProviderSelectionTests
{
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
        var client = provider.GetRequiredService<ILibsqlDatabaseClient>();

        await Assert.That(configEntries.GetType().FullName)
            .IsEqualTo(typeof(LibsqlConfigEntryRepository).FullName);
        await Assert.That(projects.GetType().FullName)
            .IsEqualTo(typeof(LibsqlProjectRepository).FullName);
        await Assert.That(client.GetType().FullName)
            .IsEqualTo(typeof(NelknetLibsqlDatabaseClient).FullName);
    }

    [Test]
    public async Task ConfigureServices_RejectsLocalReplicaOption()
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
        Exception? exception = null;
        try
        {
            _ = provider.GetRequiredService<IOptions<LibsqlOptions>>().Value;
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        await Assert.That(exception).IsNotNull();
        await Assert.That(exception).IsTypeOf<OptionsValidationException>();
        await Assert.That(exception!.Message.Contains("EnableLocalReplica", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task ConfigureServices_SelectsDirectLibsqlClient_WhenConfigured()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:Type"] = "Libsql",
                ["ConnectionStrings:Libsql"] = "http://127.0.0.1:9999",
                ["Storage:Libsql:TimeoutSeconds"] = "15"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddInfrastructureServices(configuration);

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<ILibsqlDatabaseClient>();

        await Assert.That(client.GetType().FullName)
            .IsEqualTo(typeof(NelknetLibsqlDatabaseClient).FullName);
    }

    [Test]
    public async Task ConfigureServices_RejectsFilePathLibsqlDataSource()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:Type"] = "Libsql",
                ["ConnectionStrings:Libsql"] = Path.Combine(Path.GetTempPath(), $"nona-libsql-local-{Guid.NewGuid():N}.db")
            })
            .Build();

        var services = new ServiceCollection();
        services.AddInfrastructureServices(configuration);

        using var provider = services.BuildServiceProvider();
        Exception? exception = null;
        try
        {
            _ = provider.GetRequiredService<IOptions<LibsqlOptions>>().Value;
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        await Assert.That(exception).IsNotNull();
        await Assert.That(exception).IsTypeOf<OptionsValidationException>();
        await Assert.That(exception!.Message.Contains("HTTP data source", StringComparison.OrdinalIgnoreCase)).IsTrue();
    }

    [Test]
    public async Task ConfigureServices_UsesManagedPrimaryLocalConnectUrl_WhenManagedPrimaryEnabled()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"nona-managed-primary-{Guid.NewGuid():N}.db");

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:Type"] = "Libsql",
                ["Storage:Libsql:ManagedPrimary:Enabled"] = "true",
                ["Storage:Libsql:ManagedPrimary:ExecutablePath"] = "sqld",
                ["Storage:Libsql:ManagedPrimary:DatabasePath"] = databasePath,
                ["Storage:Libsql:ManagedPrimary:HttpListenAddress"] = "0.0.0.0:9180"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddInfrastructureServices(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<LibsqlOptions>>().Value;

        await Assert.That(options.ManagedPrimary.Enabled).IsTrue();
        await Assert.That(options.DataSource).IsEqualTo("http://127.0.0.1:9180");
    }

    [Test]
    public async Task ConfigureServices_RejectsManagedPrimaryReplicaCombination()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:Type"] = "Libsql",
                ["Storage:Libsql:ManagedPrimary:Enabled"] = "true",
                ["Storage:Libsql:ManagedPrimary:ExecutablePath"] = "sqld",
                ["Storage:Libsql:ManagedPrimary:DatabasePath"] = Path.Combine(Path.GetTempPath(), $"nona-managed-primary-{Guid.NewGuid():N}.db"),
                ["Storage:Libsql:ManagedPrimary:HttpListenAddress"] = "127.0.0.1:9180",
                ["Storage:Libsql:EnableLocalReplica"] = "true",
                ["Storage:Libsql:LocalReplicaPath"] = Path.Combine(Path.GetTempPath(), $"nona-managed-replica-{Guid.NewGuid():N}.db")
            })
            .Build();

        var services = new ServiceCollection();
        services.AddInfrastructureServices(configuration);

        using var provider = services.BuildServiceProvider();

        Exception? exception = null;
        try
        {
            _ = provider.GetRequiredService<IOptions<LibsqlOptions>>().Value;
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        await Assert.That(exception).IsNotNull();
        await Assert.That(exception).IsTypeOf<OptionsValidationException>();
    }

    [Test]
    public async Task ConfigureServices_RequiresLibsqlDataSource_WhenConfiguredForLibsql()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:Type"] = "Libsql"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddInfrastructureServices(configuration);

        using var provider = services.BuildServiceProvider();

        Exception? exception = null;
        try
        {
            _ = provider.GetRequiredService<NelknetLibsqlDatabaseClient>();
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.Message.Contains("Libsql", StringComparison.OrdinalIgnoreCase)).IsTrue();
    }
}
