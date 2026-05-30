using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Nona.Cli;

internal sealed class CliSessionStore
{
    private const string SessionPathEnvironmentVariable = "NONA_CLI_SESSION_PATH";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public CliSessionStore(string? filePath = null)
    {
        FilePath = filePath ?? ResolvePath();
    }

    public string FilePath { get; }

    public CliAuthSession? Load()
    {
        if (!File.Exists(FilePath))
            return null;

        var bytes = File.ReadAllBytes(FilePath);
        if (bytes.Length == 0)
            return null;

        try
        {
            var json = OperatingSystem.IsWindows()
                ? Encoding.UTF8.GetString(ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser))
                : Encoding.UTF8.GetString(bytes);

            return JsonSerializer.Deserialize<CliAuthSession>(json, SerializerOptions);
        }
        catch
        {
            return null;
        }
    }

    public void Save(CliAuthSession session)
    {
        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(session, SerializerOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        var persistedBytes = OperatingSystem.IsWindows()
            ? ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser)
            : bytes;

        File.WriteAllBytes(FilePath, persistedBytes);

        if (!OperatingSystem.IsWindows())
            TryApplyUnixPermissions(FilePath);
    }

    public void Clear()
    {
        if (File.Exists(FilePath))
            File.Delete(FilePath);
    }

    private static string ResolvePath()
    {
        var overridePath = Environment.GetEnvironmentVariable(SessionPathEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(overridePath))
            return Path.GetFullPath(overridePath);

        var baseDirectory = CliStoragePaths.ResolveBaseDirectory();
        return Path.Combine(baseDirectory, "session.json");
    }

    private static void TryApplyUnixPermissions(string path)
    {
        try
        {
            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() || OperatingSystem.IsFreeBSD())
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch
        {
            // Best effort only.
        }
    }
}

internal sealed record CliAuthSession
{
    public string BaseUrl { get; init; } = string.Empty;
    public string Token { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }
    public DateTime SavedAtUtc { get; init; }

    public bool IsExpired => ExpiresAt <= DateTime.UtcNow;

    public bool MatchesBaseUrl(string baseUrl)
    {
        return string.Equals(
            BaseUrl.TrimEnd('/'),
            baseUrl.TrimEnd('/'),
            StringComparison.OrdinalIgnoreCase);
    }
}
