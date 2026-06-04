namespace Nona.Libsql;

public sealed class LibsqlManagedPrimaryOptions
{
    public bool Enabled { get; set; }
    public string ExecutablePath { get; set; } = "sqld";
    public string DatabasePath { get; set; } = string.Empty;
    public string HttpListenAddress { get; set; } = "127.0.0.1:9080";
    public string LocalConnectUrl { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public int StartTimeoutSeconds { get; set; } = 30;
    public string[] ExtraArgs { get; set; } = [];

    public string ResolveDatabasePath()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(DatabasePath);
        return Path.IsPathRooted(DatabasePath)
            ? DatabasePath
            : Path.GetFullPath(DatabasePath);
    }

    public string ResolveWorkingDirectory()
    {
        if (!string.IsNullOrWhiteSpace(WorkingDirectory))
        {
            return Path.IsPathRooted(WorkingDirectory)
                ? WorkingDirectory
                : Path.GetFullPath(WorkingDirectory);
        }

        var databaseDirectory = Path.GetDirectoryName(ResolveDatabasePath());
        return string.IsNullOrWhiteSpace(databaseDirectory)
            ? Directory.GetCurrentDirectory()
            : databaseDirectory;
    }

    public string ResolveLocalConnectUrl()
    {
        if (!string.IsNullOrWhiteSpace(LocalConnectUrl))
        {
            return LocalConnectUrl.Trim();
        }

        var (host, port) = ParseListenAddress(HttpListenAddress);
        return $"http://{NormalizeLocalConnectHost(host)}:{port}";
    }

    private static (string Host, int Port) ParseListenAddress(string listenAddress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(listenAddress);

        var trimmed = listenAddress.Trim();
        if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(trimmed, UriKind.Absolute);
            return (uri.Host, uri.Port);
        }

        if (trimmed.StartsWith("[", StringComparison.Ordinal))
        {
            var closingBracket = trimmed.IndexOf(']');
            if (closingBracket <= 1 || closingBracket + 2 > trimmed.Length || trimmed[closingBracket + 1] != ':')
            {
                throw new InvalidOperationException($"Invalid listen address '{listenAddress}'.");
            }

            return (
                trimmed[1..closingBracket],
                int.Parse(trimmed[(closingBracket + 2)..], System.Globalization.CultureInfo.InvariantCulture));
        }

        var separatorIndex = trimmed.LastIndexOf(':');
        if (separatorIndex <= 0 || separatorIndex == trimmed.Length - 1)
        {
            throw new InvalidOperationException($"Invalid listen address '{listenAddress}'.");
        }

        return (
            trimmed[..separatorIndex],
            int.Parse(trimmed[(separatorIndex + 1)..], System.Globalization.CultureInfo.InvariantCulture));
    }

    private static string NormalizeLocalConnectHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host)
            || host == "0.0.0.0"
            || host == "*"
            || host == "::"
            || host == "[::]")
        {
            return "127.0.0.1";
        }

        return host;
    }
}
