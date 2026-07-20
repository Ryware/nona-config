using Microsoft.Extensions.Configuration;
using Nona.Infrastructure.Configuration;

namespace Nona.Infrastructure.Tests;

public class StorageProviderResolverTests
{
    [Test]
    public async Task Resolve_NoReplicationFlags_SelectsSqlite()
    {
        var resolution = Resolve(new Dictionary<string, string?>
        {
            ["Storage:Type"] = "Auto"
        });

        await Assert.That(resolution.Provider).IsEqualTo(StorageProviderKind.Sqlite);
        await Assert.That(resolution.DeploymentMode).IsEqualTo(StorageDeploymentMode.Standalone);
        await Assert.That(resolution.Message)
            .IsEqualTo("Storage provider resolved to SQLite: no replication configuration detected.");
    }

    [Test]
    public async Task Resolve_PrimaryComposeArguments_SelectsLibsqlPrimary()
    {
        var resolution = Resolve(new Dictionary<string, string?>
        {
            ["Storage:Type"] = "Auto",
            ["Storage:Libsql:ManagedPrimary:ExtraArgs:0"] = "--grpc-listen-addr",
            ["Storage:Libsql:ManagedPrimary:ExtraArgs:1"] = "0.0.0.0:5001"
        });

        await Assert.That(resolution.Provider).IsEqualTo(StorageProviderKind.Libsql);
        await Assert.That(resolution.DeploymentMode).IsEqualTo(StorageDeploymentMode.Primary);
        await Assert.That(resolution.UseManagedLibsql).IsTrue();
        await Assert.That(resolution.Message)
            .IsEqualTo("Storage provider resolved to libSQL Primary: --grpc-listen-addr detected.");
    }

    [Test]
    public async Task Resolve_ReplicaComposeArguments_SelectsLibsqlReplica()
    {
        var resolution = Resolve(new Dictionary<string, string?>
        {
            ["Storage:Type"] = "Auto",
            ["Storage:Libsql:ManagedPrimary:ExtraArgs:0"] = "--primary-grpc-url",
            ["Storage:Libsql:ManagedPrimary:ExtraArgs:1"] = "http://nona-primary:5001"
        });

        await Assert.That(resolution.Provider).IsEqualTo(StorageProviderKind.Libsql);
        await Assert.That(resolution.DeploymentMode).IsEqualTo(StorageDeploymentMode.Replica);
        await Assert.That(resolution.UseManagedLibsql).IsTrue();
        await Assert.That(resolution.Message)
            .IsEqualTo("Storage provider resolved to libSQL Replica: --primary-grpc-url detected.");
    }

    [Test]
    public async Task Resolve_PrimaryOptionEqualsForm_SelectsLibsqlPrimary()
    {
        var resolution = Resolve(new Dictionary<string, string?>
        {
            ["Storage:Type"] = "Auto",
            ["Storage:Libsql:ManagedPrimary:ExtraArgs:0"] = "--grpc-listen-addr=0.0.0.0:5001"
        });

        await Assert.That(resolution.DeploymentMode).IsEqualTo(StorageDeploymentMode.Primary);
    }

    [Test]
    public async Task Resolve_ReplicaOptionEqualsForm_SelectsLibsqlReplica()
    {
        var resolution = Resolve(new Dictionary<string, string?>
        {
            ["Storage:Type"] = "Auto",
            ["Storage:Libsql:ManagedPrimary:ExtraArgs:0"] = "--primary-grpc-url=http://nona-primary:5001"
        });

        await Assert.That(resolution.DeploymentMode).IsEqualTo(StorageDeploymentMode.Replica);
    }

    [Test]
    public async Task Resolve_RemoteLibsqlDataSource_SelectsRemoteLibsql()
    {
        var resolution = Resolve(new Dictionary<string, string?>
        {
            ["Storage:Type"] = "Auto",
            ["ConnectionStrings:Libsql"] = "libsql://database.example"
        });

        await Assert.That(resolution.Provider).IsEqualTo(StorageProviderKind.Libsql);
        await Assert.That(resolution.DeploymentMode).IsEqualTo(StorageDeploymentMode.Remote);
        await Assert.That(resolution.UseManagedLibsql).IsFalse();
    }

    [Test]
    public async Task Resolve_RemoteStorageLibsqlDataSource_SelectsRemoteLibsql()
    {
        var resolution = Resolve(new Dictionary<string, string?>
        {
            ["Storage:Type"] = "Auto",
            ["Storage:Libsql:DataSource"] = "https://database.example"
        });

        await Assert.That(resolution.Provider).IsEqualTo(StorageProviderKind.Libsql);
        await Assert.That(resolution.DeploymentMode).IsEqualTo(StorageDeploymentMode.Remote);
    }

    [Test]
    public async Task Resolve_ExplicitLibsqlOverride_SelectsLibsql()
    {
        var resolution = Resolve(new Dictionary<string, string?>
        {
            ["Storage:Type"] = "Libsql",
            ["Storage:Libsql:ManagedPrimary:Enabled"] = "true"
        });

        await Assert.That(resolution.Provider).IsEqualTo(StorageProviderKind.Libsql);
        await Assert.That(resolution.DeploymentMode).IsEqualTo(StorageDeploymentMode.Explicit);
        await Assert.That(resolution.UseManagedLibsql).IsTrue();
    }

    [Test]
    public async Task Resolve_ExplicitSqliteWithReplicationMarker_RejectsStartup()
    {
        Exception? exception = null;
        try
        {
            _ = Resolve(new Dictionary<string, string?>
            {
                ["Storage:Type"] = "Sqlite",
                ["Storage:Libsql:ManagedPrimary:ExtraArgs:0"] = "--primary-grpc-url",
                ["Storage:Libsql:ManagedPrimary:ExtraArgs:1"] = "http://nona-primary:5001"
            });
        }
        catch (Exception caught)
        {
            exception = caught;
        }

        await Assert.That(exception).IsNotNull();
        await Assert.That(exception).IsTypeOf<InvalidOperationException>();
        await Assert.That(exception!.Message.Contains("--primary-grpc-url", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task Resolve_UnrelatedSqldArgumentsAlone_SelectsSqlite()
    {
        var resolution = Resolve(new Dictionary<string, string?>
        {
            ["Storage:Type"] = "Auto",
            ["Storage:Libsql:ManagedPrimary:ExtraArgs:0"] = "--max-concurrent-connections",
            ["Storage:Libsql:ManagedPrimary:ExtraArgs:1"] = "4096",
            ["Storage:Libsql:ManagedPrimary:ExtraArgs:2"] = "--http-self-url",
            ["Storage:Libsql:ManagedPrimary:ExtraArgs:3"] = "http://nona:9080"
        });

        await Assert.That(resolution.Provider).IsEqualTo(StorageProviderKind.Sqlite);
        await Assert.That(resolution.DeploymentMode).IsEqualTo(StorageDeploymentMode.Standalone);
    }

    private static StorageProviderResolution Resolve(IReadOnlyDictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        return StorageProviderResolver.Resolve(configuration);
    }
}
