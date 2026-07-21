namespace Nona.Libsql;

public sealed class SqliteOptions
{
    public string DataSource { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 30;
}
