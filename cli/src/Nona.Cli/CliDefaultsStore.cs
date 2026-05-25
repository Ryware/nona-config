using System.Text.Json;

namespace Nona.Cli;

internal sealed class CliDefaultsStore
{
    private const string ConfigPathEnvironmentVariable = "NONA_CLI_CONFIG_PATH";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public CliDefaultsStore(string? filePath = null)
    {
        FilePath = filePath ?? ResolvePath();
    }

    public string FilePath { get; }

    public CliDefaults Load()
    {
        if (!File.Exists(FilePath))
            return CliDefaults.Empty;

        var content = File.ReadAllText(FilePath);
        if (string.IsNullOrWhiteSpace(content))
            return CliDefaults.Empty;

        return JsonSerializer.Deserialize<CliDefaults>(content, SerializerOptions) ?? CliDefaults.Empty;
    }

    public void Save(CliDefaults defaults)
    {
        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var content = JsonSerializer.Serialize(defaults, SerializerOptions);
        File.WriteAllText(FilePath, content);
    }

    private static string ResolvePath()
    {
        var overridePath = Environment.GetEnvironmentVariable(ConfigPathEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(overridePath))
            return Path.GetFullPath(overridePath);

        return Path.Combine(CliStoragePaths.ResolveBaseDirectory(), "config.json");
    }
}

internal sealed record CliDefaults
{
    public static CliDefaults Empty { get; } = new();

    public string? BaseUrl { get; init; }

    public string? Project { get; init; }
}
