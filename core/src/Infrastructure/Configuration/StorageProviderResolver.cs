using Microsoft.Extensions.Configuration;

namespace Nona.Infrastructure.Configuration;

public enum StorageProviderKind
{
    Sqlite,
    Libsql,
    InMemory
}

public enum StorageDeploymentMode
{
    Standalone,
    Primary,
    Replica,
    Remote,
    Explicit
}

public sealed record StorageProviderResolution(
    StorageProviderKind Provider,
    StorageDeploymentMode DeploymentMode,
    bool UseManagedLibsql);

public static class StorageProviderResolver
{
    private const string PrimaryMarker = "--grpc-listen-addr";
    private const string ReplicaMarker = "--primary-grpc-url";

    public static StorageProviderResolution Resolve(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var storageType = ConfigurationValueReader.GetString(configuration, "Storage:Type", "Auto").Trim();
        var extraArgs = ConfigurationValueReader.GetStringList(
            configuration,
            "Storage:Libsql:ManagedPrimary:ExtraArgs");
        var hasPrimaryMarker = HasOption(extraArgs, PrimaryMarker);
        var hasReplicaMarker = HasOption(extraArgs, ReplicaMarker);

        if (hasPrimaryMarker && hasReplicaMarker)
        {
            throw new InvalidOperationException(
                $"Storage configuration cannot contain both {PrimaryMarker} and {ReplicaMarker}.");
        }

        if (storageType.Equals("Auto", StringComparison.OrdinalIgnoreCase))
        {
            return ResolveAuto(configuration, hasPrimaryMarker, hasReplicaMarker);
        }

        if (storageType.Equals("Libsql", StringComparison.OrdinalIgnoreCase))
        {
            return ResolveLibsql(configuration, hasPrimaryMarker, hasReplicaMarker);
        }

        if (storageType.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            RejectReplicationMarkers("SQLite", hasPrimaryMarker, hasReplicaMarker);
            return new StorageProviderResolution(
                StorageProviderKind.Sqlite,
                StorageDeploymentMode.Explicit,
                UseManagedLibsql: false);
        }

        if (storageType.Equals("InMemory", StringComparison.OrdinalIgnoreCase))
        {
            RejectReplicationMarkers("InMemory", hasPrimaryMarker, hasReplicaMarker);
            return new StorageProviderResolution(
                StorageProviderKind.InMemory,
                StorageDeploymentMode.Explicit,
                UseManagedLibsql: false);
        }

        throw new InvalidOperationException(
            $"Unsupported Storage:Type '{storageType}'. Expected Auto, Sqlite, Libsql, or InMemory.");
    }

    private static StorageProviderResolution ResolveAuto(
        IConfiguration configuration,
        bool hasPrimaryMarker,
        bool hasReplicaMarker)
    {
        if (hasPrimaryMarker)
        {
            return CreatePrimaryResolution();
        }

        if (hasReplicaMarker)
        {
            return CreateReplicaResolution();
        }

        if (HasRemoteLibsqlDataSource(configuration))
        {
            return new StorageProviderResolution(
                StorageProviderKind.Libsql,
                StorageDeploymentMode.Remote,
                UseManagedLibsql: false);
        }

        return new StorageProviderResolution(
            StorageProviderKind.Sqlite,
            StorageDeploymentMode.Standalone,
            UseManagedLibsql: false);
    }

    private static StorageProviderResolution ResolveLibsql(
        IConfiguration configuration,
        bool hasPrimaryMarker,
        bool hasReplicaMarker)
    {
        if (hasPrimaryMarker)
        {
            return CreatePrimaryResolution();
        }

        if (hasReplicaMarker)
        {
            return CreateReplicaResolution();
        }

        if (HasRemoteLibsqlDataSource(configuration))
        {
            return new StorageProviderResolution(
                StorageProviderKind.Libsql,
                StorageDeploymentMode.Remote,
                UseManagedLibsql: false);
        }

        return new StorageProviderResolution(
            StorageProviderKind.Libsql,
            StorageDeploymentMode.Explicit,
            ConfigurationValueReader.GetBoolean(configuration, "Storage:Libsql:ManagedPrimary:Enabled"));
    }

    private static StorageProviderResolution CreatePrimaryResolution()
    {
        return new StorageProviderResolution(
            StorageProviderKind.Libsql,
            StorageDeploymentMode.Primary,
            UseManagedLibsql: true);
    }

    private static StorageProviderResolution CreateReplicaResolution()
    {
        return new StorageProviderResolution(
            StorageProviderKind.Libsql,
            StorageDeploymentMode.Replica,
            UseManagedLibsql: true);
    }

    private static bool HasRemoteLibsqlDataSource(IConfiguration configuration)
    {
        return IsRemoteLibsqlDataSource(configuration["ConnectionStrings:Libsql"])
            || IsRemoteLibsqlDataSource(configuration["Storage:Libsql:DataSource"]);
    }

    internal static bool IsRemoteLibsqlDataSource(string? dataSource)
    {
        return dataSource?.StartsWith("http://", StringComparison.OrdinalIgnoreCase) == true
            || dataSource?.StartsWith("https://", StringComparison.OrdinalIgnoreCase) == true
            || dataSource?.StartsWith("libsql://", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool HasOption(IEnumerable<string> arguments, string option)
    {
        return arguments.Any(argument =>
            argument.Equals(option, StringComparison.Ordinal)
            || argument.StartsWith($"{option}=", StringComparison.Ordinal));
    }

    private static void RejectReplicationMarkers(
        string provider,
        bool hasPrimaryMarker,
        bool hasReplicaMarker)
    {
        if (!hasPrimaryMarker && !hasReplicaMarker)
        {
            return;
        }

        var marker = hasPrimaryMarker ? PrimaryMarker : ReplicaMarker;
        throw new InvalidOperationException(
            $"Storage:Type={provider} cannot be used when replication marker {marker} is configured.");
    }
}
