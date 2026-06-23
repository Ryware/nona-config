using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nona.Infrastructure.Configuration;

public static partial class PersistentJwtConfiguration
{
    private const string GeneratedFileName = "jwt.generated.json";
    private const string DefaultIssuer = "nona";
    private const string DefaultAudience = "nona";

    private static readonly string[] RequiredConfigurationKeys =
    [
        "Jwt:Key",
        "Jwt:Issuer",
        "Jwt:Audience"
    ];

    private static readonly string[] RequiredEnvironmentVariables =
    [
        "Jwt__Key",
        "Jwt__Issuer",
        "Jwt__Audience"
    ];

    public static void Apply(ConfigurationManager configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (HasCompleteJwtEnvironmentOverride())
        {
            if (TryResolveGeneratedConfigPath(configuration, out var generatedConfigPath))
            {
                DeleteGeneratedConfig(generatedConfigPath);
            }

            return;
        }

        if (HasCompleteJwtConfiguration(configuration))
        {
            return;
        }

        var generatedSettings = ShouldPersistGeneratedSettings(configuration)
            ? ReadOrCreateGeneratedSettings(ResolveGeneratedConfigPath(configuration))
            : CreateGeneratedSettings();

        configuration.AddInMemoryCollection(CreateMissingConfigurationValues(configuration, generatedSettings));
    }

    private static bool HasCompleteJwtEnvironmentOverride()
    {
        return RequiredEnvironmentVariables.All(name =>
            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name)));
    }

    private static bool HasCompleteJwtConfiguration(IConfiguration configuration)
    {
        return RequiredConfigurationKeys.All(key => !string.IsNullOrWhiteSpace(configuration[key]));
    }

    private static IReadOnlyDictionary<string, string?> CreateMissingConfigurationValues(
        IConfiguration configuration,
        JwtSettings settings)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        AddIfMissing(values, configuration, "Jwt:Key", settings.Key);
        AddIfMissing(values, configuration, "Jwt:Issuer", settings.Issuer);
        AddIfMissing(values, configuration, "Jwt:Audience", settings.Audience);

        return values;
    }

    private static void AddIfMissing(
        IDictionary<string, string?> values,
        IConfiguration configuration,
        string key,
        string value)
    {
        if (string.IsNullOrWhiteSpace(configuration[key]))
        {
            values[key] = value;
        }
    }

    private static JwtSettings ReadOrCreateGeneratedSettings(string generatedConfigPath)
    {
        if (TryReadGeneratedSettings(generatedConfigPath, out var settings))
        {
            return settings;
        }

        settings = CreateGeneratedSettings();

        if (!File.Exists(generatedConfigPath))
        {
            if (TryWriteGeneratedSettings(generatedConfigPath, settings, overwrite: false))
            {
                return settings;
            }

            if (TryReadGeneratedSettings(generatedConfigPath, out var existingSettings))
            {
                return existingSettings;
            }
        }

        WriteGeneratedSettings(generatedConfigPath, settings);
        return settings;
    }

    private static JwtSettings CreateGeneratedSettings()
    {
        return new JwtSettings(
            Key: Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
            Issuer: DefaultIssuer,
            Audience: DefaultAudience);
    }

    private static bool TryReadGeneratedSettings(string generatedConfigPath, out JwtSettings settings)
    {
        settings = default;

        if (!File.Exists(generatedConfigPath))
        {
            return false;
        }

        try
        {
            using var stream = File.OpenRead(generatedConfigPath);
            var persisted = JsonSerializer.Deserialize(
                stream,
                PersistentJwtJsonSerializerContext.Default.PersistedJwtConfiguration);

            if (persisted is null || !IsComplete(persisted.Jwt))
            {
                return false;
            }

            settings = persisted.Jwt;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static void WriteGeneratedSettings(string generatedConfigPath, JwtSettings settings)
    {
        _ = TryWriteGeneratedSettings(generatedConfigPath, settings, overwrite: true);
    }

    private static bool TryWriteGeneratedSettings(string generatedConfigPath, JwtSettings settings, bool overwrite)
    {
        var directory = Path.GetDirectoryName(generatedConfigPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(
            new PersistedJwtConfiguration(settings),
            PersistentJwtJsonSerializerContext.Default.PersistedJwtConfiguration);

        var temporaryPath = $"{generatedConfigPath}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(temporaryPath, json);
        try
        {
            File.Move(temporaryPath, generatedConfigPath, overwrite);
            return true;
        }
        catch (IOException) when (!overwrite && File.Exists(generatedConfigPath))
        {
            File.Delete(temporaryPath);
            return false;
        }
        catch
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            throw;
        }
    }

    private static void DeleteGeneratedConfig(string generatedConfigPath)
    {
        if (File.Exists(generatedConfigPath))
        {
            File.Delete(generatedConfigPath);
        }
    }

    private static bool IsComplete(JwtSettings settings)
    {
        return !string.IsNullOrWhiteSpace(settings.Key)
            && !string.IsNullOrWhiteSpace(settings.Issuer)
            && !string.IsNullOrWhiteSpace(settings.Audience);
    }

    private static bool ShouldPersistGeneratedSettings(IConfiguration configuration)
    {
        var storageType = ConfigurationValueReader.GetString(configuration, "Storage:Type", "InMemory");

        return storageType.Equals("Libsql", StringComparison.OrdinalIgnoreCase)
            && ConfigurationValueReader.GetBoolean(configuration, "Storage:Libsql:ManagedPrimary:Enabled");
    }

    private static bool TryResolveGeneratedConfigPath(IConfiguration configuration, out string generatedConfigPath)
    {
        generatedConfigPath = string.Empty;

        var databasePath = configuration["Storage:Libsql:ManagedPrimary:DatabasePath"];
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            return false;
        }

        generatedConfigPath = Path.Combine(GetDirectoryName(ResolvePath(databasePath)), GeneratedFileName);
        return true;
    }

    private static string ResolveGeneratedConfigPath(IConfiguration configuration)
    {
        if (!TryResolveGeneratedConfigPath(configuration, out var generatedConfigPath))
        {
            throw new InvalidOperationException(
                "Storage:Libsql:ManagedPrimary:DatabasePath must be configured to persist generated JWT settings.");
        }

        return generatedConfigPath;
    }

    private static string ResolvePath(string path)
    {
        return Path.IsPathRooted(path) ? path : Path.GetFullPath(path);
    }

    private static string GetDirectoryName(string path)
    {
        var directory = Path.GetDirectoryName(path);
        return string.IsNullOrWhiteSpace(directory)
            ? Directory.GetCurrentDirectory()
            : directory;
    }

    internal sealed record PersistedJwtConfiguration(JwtSettings Jwt);

    internal readonly record struct JwtSettings(string Key, string Issuer, string Audience);

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(PersistedJwtConfiguration))]
    internal sealed partial class PersistentJwtJsonSerializerContext : JsonSerializerContext;
}
