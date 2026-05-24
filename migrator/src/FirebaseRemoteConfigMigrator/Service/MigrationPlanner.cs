using Nona.FirebaseRemoteConfigMigrator.Models;
using Nona.Migrator.Core.Models;

namespace Nona.Migrator.FirebaseRemoteConfig.Service;

internal static class MigrationPlanner
{
    public static MigrationPlan Build(FirebaseRemoteConfigTemplate template, MigrationOptions options, string scope)
    {
        var warnings = new List<string>();
        var orderedConditions = (template.Conditions ?? [])
            .Where(static condition => condition is not null)
            .Select(static condition => condition!.Name)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .ToArray();

        var targetEnvironments = new HashSet<string>(options.DefaultValueEnvironments, StringComparer.OrdinalIgnoreCase);
        foreach (var environmentName in options.ConditionEnvironmentMappings.Values)
        {
            if (!string.IsNullOrWhiteSpace(environmentName))
                targetEnvironments.Add(environmentName);
        }

        var flattenedParameters = template.GetAllParameters(warnings);
        var entries = new List<PlannedConfigEntry>();

        foreach (var (key, parameter) in flattenedParameters.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            var contentType = ResolveContentType(key, parameter.ValueType, warnings);

            foreach (var environment in targetEnvironments.OrderBy(static value => value, StringComparer.OrdinalIgnoreCase))
            {
                var conditionalMatch = FindConditionalMatch(key, environment, parameter, orderedConditions, options.ConditionEnvironmentMappings, warnings);
                if (conditionalMatch is not null)
                {
                    entries.Add(new PlannedConfigEntry(environment, key, conditionalMatch.Value, contentType, scope, conditionalMatch.SourceLabel));
                    continue;
                }

                if (ShouldApplyDefault(environment, options)
                    && parameter.DefaultValue?.TryGetExplicitValue(out var defaultValue) == true)
                {
                    entries.Add(new PlannedConfigEntry(environment, key, defaultValue, contentType, scope, "defaultValue"));
                }
            }

            foreach (var conditionName in (parameter.ConditionalValues ?? new Dictionary<string, FirebaseValue>(StringComparer.OrdinalIgnoreCase))
                .Keys
                .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase))
            {
                if (!options.ConditionEnvironmentMappings.ContainsKey(conditionName))
                    warnings.Add($"Skipped unmapped Firebase condition '{conditionName}' for key '{key}'.");
            }
        }

        return new MigrationPlan(
            targetEnvironments.OrderBy(static value => value, StringComparer.OrdinalIgnoreCase).ToArray(),
            entries,
            warnings.Distinct(StringComparer.Ordinal).ToArray(),
            flattenedParameters.Count);
    }

    private static string ResolveContentType(
        string key,
        string? firebaseValueType,
        ICollection<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(firebaseValueType))
            return "string";

        return firebaseValueType.Trim().ToUpperInvariant() switch
        {
            "STRING" => "string",
            "BOOLEAN" => "boolean",
            "NUMBER" => "number",
            "JSON" => "json",
            "PARAMETER_VALUE_TYPE_UNSPECIFIED" => "string",
            _ => WarnAndUseFallback(key, firebaseValueType, warnings)
        };
    }

    private static string WarnAndUseFallback(
        string key,
        string firebaseValueType,
        ICollection<string> warnings)
    {
        warnings.Add(
            $"Unknown Firebase valueType '{firebaseValueType}' for key '{key}'. " +
            "Using fallback content type 'string'.");
        return "string";
    }

    private static bool ShouldApplyDefault(string environment, MigrationOptions options)
    {
        return options.DefaultValueEnvironments.Contains(environment, StringComparer.OrdinalIgnoreCase)
            || (options.ApplyDefaultToMappedEnvironments
                && options.ConditionEnvironmentMappings.Values.Contains(environment, StringComparer.OrdinalIgnoreCase));
    }

    private static PlannedValue? FindConditionalMatch(
        string key,
        string environment,
        FirebaseParameter parameter,
        IReadOnlyList<string> orderedConditions,
        IReadOnlyDictionary<string, string> mappings,
        ICollection<string> warnings)
    {
        foreach (var conditionName in orderedConditions)
        {
            if (!mappings.TryGetValue(conditionName, out var mappedEnvironment))
                continue;

            if (!string.Equals(mappedEnvironment, environment, StringComparison.OrdinalIgnoreCase))
                continue;

            var conditionalValues = parameter.ConditionalValues ?? new Dictionary<string, FirebaseValue>(StringComparer.OrdinalIgnoreCase);
            if (!conditionalValues.TryGetValue(conditionName, out var conditionalValue))
                continue;

            if (!conditionalValue.TryGetExplicitValue(out var value))
            {
                warnings.Add($"Skipped non-literal conditional value '{conditionName}' for key '{key}'.");
                continue;
            }

            return new PlannedValue(value, $"condition:{conditionName}");
        }

        return null;
    }

    private sealed record PlannedValue(string Value, string SourceLabel);
}
