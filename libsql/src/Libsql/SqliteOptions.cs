namespace Nona.Libsql;

public sealed class SqliteOptions
{
    public string DataSource { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 30;
    public string LegacySqldDatabasePath { get; set; } = "/var/lib/nona/primary.db";
}
