using Nona.FirebaseRemoteConfigMigrator.Options;
using System.Text.Json;

namespace Nona.FirebaseRemoteConfigMigrator;

internal sealed class MigrationConfiguration
{
    public FirebaseOptions Firebase { get; set; } = new();
    public NonaOptions Nona { get; set; } = new();
    public MigrationOptions Migration { get; set; } = new();

    public static async Task<MigrationConfiguration> LoadAsync(string[] args, CancellationToken cancellationToken)
    {
        var (configPath, dryRunOverride) = ParseArguments(args);
        var effectiveConfigPath = Environment.GetEnvironmentVariable("NONA_MIGRATOR_CONFIG_PATH")
            ?? configPath
            ?? Path.Combine(AppContext.BaseDirectory, "appsettings.json");

        var configuration = File.Exists(effectiveConfigPath)
            ? await LoadFromFileAsync(effectiveConfigPath, cancellationToken)
            : new MigrationConfiguration();

        configuration.ApplyEnvironmentOverrides();

        if (dryRunOverride.HasValue)
            configuration.Migration = configuration.Migration with { DryRun = dryRunOverride.Value };

        return configuration;
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Firebase.ProjectId))
            throw new InvalidOperationException("Firebase:ProjectId required.");

        if (string.IsNullOrWhiteSpace(Firebase.ServiceAccountJson) && string.IsNullOrWhiteSpace(Firebase.ServiceAccountJsonPath))
            throw new InvalidOperationException("Firebase service account json or path required.");

        if (string.IsNullOrWhiteSpace(Nona.BaseUrl))
            throw new InvalidOperationException("Nona:BaseUrl required.");

        if (string.IsNullOrWhiteSpace(Nona.ProjectName))
            throw new InvalidOperationException("Nona:ProjectName required.");

        var hasBearerToken = !string.IsNullOrWhiteSpace(Nona.BearerToken);
        var hasEmailPassword = !string.IsNullOrWhiteSpace(Nona.Email) && !string.IsNullOrWhiteSpace(Nona.Password);
        if (!hasBearerToken && !hasEmailPassword)
            throw new InvalidOperationException("Set Nona bearer token or email/password.");

        var hasMappedEnvironments = Migration.ConditionEnvironmentMappings.Count > 0;
        var hasDefaultEnvironments = Migration.DefaultValueEnvironments.Count > 0;
        if (!hasMappedEnvironments && !hasDefaultEnvironments)
            throw new InvalidOperationException("Need default envs or Firebase condition->Nona env mappings.");

        foreach (var source in Firebase.GetImportSources())
        {
            if (!IsValidScope(source.Scope))
                throw new InvalidOperationException($"Invalid Firebase source scope '{source.Scope}'. Must be client/server/all.");
        }
    }

    private static async Task<MigrationConfiguration> LoadFromFileAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var configuration = await JsonSerializer.DeserializeAsync(stream, SerializerContext.Default.MigrationConfiguration, cancellationToken);

        return configuration ?? new MigrationConfiguration();
    }

    private void ApplyEnvironmentOverrides()
    {
        Firebase = Firebase with
        {
            ProjectId = GetValue("NONA_MIGRATOR_FIREBASE_PROJECT_ID", Firebase.ProjectId) ?? Firebase.ProjectId,
            ServiceAccountJsonPath = GetValue("NONA_MIGRATOR_FIREBASE_SERVICE_ACCOUNT_PATH", Firebase.ServiceAccountJsonPath),
            ServiceAccountJson = GetValue("NONA_MIGRATOR_FIREBASE_SERVICE_ACCOUNT_JSON", Firebase.ServiceAccountJson)
        };

        Nona = Nona with
        {
            BaseUrl = GetValue("NONA_MIGRATOR_NONA_BASE_URL", Nona.BaseUrl) ?? Nona.BaseUrl,
            ProjectName = GetValue("NONA_MIGRATOR_NONA_PROJECT_NAME", Nona.ProjectName) ?? Nona.ProjectName,
            Email = GetValue("NONA_MIGRATOR_NONA_EMAIL", Nona.Email),
            Password = GetValue("NONA_MIGRATOR_NONA_PASSWORD", Nona.Password),
            BearerToken = GetValue("NONA_MIGRATOR_NONA_BEARER_TOKEN", Nona.BearerToken)
        };

        Migration = Migration with
        {
            DryRun = TryParseBool(Environment.GetEnvironmentVariable("NONA_MIGRATOR_DRY_RUN")) ?? Migration.DryRun,
            RenameConflictingKeys = TryParseBool(Environment.GetEnvironmentVariable("NONA_MIGRATOR_RENAME_CONFLICTING_KEYS"))
                ?? Migration.RenameConflictingKeys,
            ApplyDefaultToMappedEnvironments = TryParseBool(Environment.GetEnvironmentVariable("NONA_MIGRATOR_APPLY_DEFAULT_TO_MAPPED_ENVIRONMENTS"))
                ?? Migration.ApplyDefaultToMappedEnvironments,
            DefaultValueEnvironments = ParseStringList(Environment.GetEnvironmentVariable("NONA_MIGRATOR_DEFAULT_ENVIRONMENTS"), Migration.DefaultValueEnvironments),
            ConditionEnvironmentMappings = ParseDictionary(
                Environment.GetEnvironmentVariable("NONA_MIGRATOR_CONDITION_ENVIRONMENT_MAP_JSON"),
                Migration.ConditionEnvironmentMappings)
        };
    }

    private static (string? ConfigPath, bool? DryRun) ParseArguments(string[] args)
    {
        string? configPath = null;
        bool? dryRun = null;

        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];

            if (string.Equals(arg, "--config", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                configPath = args[++index];
                continue;
            }

            if (string.Equals(arg, "--dry-run", StringComparison.OrdinalIgnoreCase))
                dryRun = true;
        }

        return (configPath, dryRun);
    }

    private static string? GetValue(string envVar, string? currentValue)
    {
        var value = Environment.GetEnvironmentVariable(envVar);
        return string.IsNullOrWhiteSpace(value) ? currentValue : value;
    }

    private static IReadOnlyList<string> ParseStringList(string? rawValue, IReadOnlyList<string> fallback)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
            return fallback;

        return rawValue
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyDictionary<string, string> ParseDictionary(string? rawValue, IReadOnlyDictionary<string, string> fallback)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
            return fallback;

        var parsed = JsonSerializer.Deserialize(rawValue, SerializerContext.Default.DictionaryStringString);
        return parsed is null
            ? fallback
            : new Dictionary<string, string>(parsed, StringComparer.OrdinalIgnoreCase);
    }

    private static bool? TryParseBool(string? rawValue)
    {
        return bool.TryParse(rawValue, out var parsed) ? parsed : null;
    }

    private static bool IsValidScope(string scope)
    {
        return scope.Equals("client", StringComparison.OrdinalIgnoreCase)
            || scope.Equals("server", StringComparison.OrdinalIgnoreCase)
            || scope.Equals("all", StringComparison.OrdinalIgnoreCase);
    }
}
