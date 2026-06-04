namespace Nona.Libsql;

public sealed class LibsqlOptions
{
    public string DataSource { get; set; } = string.Empty;
    public string AuthToken { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 30;
    public bool EnableLocalReplica { get; set; }
    public string LocalReplicaPath { get; set; } = string.Empty;
    public double LocalReplicaSyncIntervalSeconds { get; set; } = 1;
    public LibsqlManagedPrimaryOptions ManagedPrimary { get; set; } = new();
}
