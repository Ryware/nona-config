using Nona.FirebaseRemoteConfigMigrator.Models;

namespace Nona.FirebaseRemoteConfigMigrator;

internal static class MigrationPlanMerger
{
    public static MigrationPlan Merge(IReadOnlyList<MigrationPlan> plans, bool renameConflictingKeys = false)
    {
        var environments = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var warnings = new List<string>();
        var parameterCount = 0;
        var mergedEntries = new Dictionary<string, PlannedConfigEntry>(StringComparer.OrdinalIgnoreCase);
        var renamedKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var plan in plans)
        {
            parameterCount += plan.ParameterCount;

            foreach (var environment in plan.Environments)
                environments.Add(environment);

            warnings.AddRange(plan.Warnings);

            foreach (var entry in plan.Entries)
            {
                var effectiveEntry = ApplyRename(entry, renamedKeys);
                var compositeKey = BuildCompositeKey(effectiveEntry.Environment, effectiveEntry.Key);
                if (!mergedEntries.TryGetValue(compositeKey, out var existing))
                {
                    mergedEntries[compositeKey] = effectiveEntry;
                    continue;
                }

                if (!string.Equals(existing.Value, effectiveEntry.Value, StringComparison.Ordinal))
                {
                    if (renameConflictingKeys)
                    {
                        var renamedKey = GetOrCreateRenamedKey(effectiveEntry, mergedEntries, renamedKeys);
                        warnings.Add(
                            $"Conflicting Firebase key '{effectiveEntry.Key}' in env '{effectiveEntry.Environment}' has different values. " +
                            $"Keeping first value from '{existing.SourceLabel}'. Entries from scope '{effectiveEntry.Scope}' will be renamed to '{renamedKey}'.");

                        effectiveEntry = effectiveEntry with { Key = renamedKey };
                        compositeKey = BuildCompositeKey(effectiveEntry.Environment, effectiveEntry.Key);
                        mergedEntries[compositeKey] = effectiveEntry;
                        continue;
                    }

                    warnings.Add(
                        $"Skipped conflicting Firebase entry for key '{effectiveEntry.Key}' in env '{effectiveEntry.Environment}'. " +
                        $"Keeping first value from '{existing.SourceLabel}', ignoring '{effectiveEntry.SourceLabel}'.");
                    continue;
                }

                if (!string.Equals(existing.ContentType, effectiveEntry.ContentType, StringComparison.OrdinalIgnoreCase))
                {
                    warnings.Add(
                        $"Skipped conflicting Firebase type for key '{effectiveEntry.Key}' in env '{effectiveEntry.Environment}'. " +
                        $"Keeping first type '{existing.ContentType}' from '{existing.SourceLabel}', ignoring '{effectiveEntry.ContentType}' from '{effectiveEntry.SourceLabel}'.");
                }

                mergedEntries[compositeKey] = existing with
                {
                    Scope = MergeScopes(existing.Scope, entry.Scope),
                    SourceLabel = $"{existing.SourceLabel} + {entry.SourceLabel}"
                };
            }
        }

        return new MigrationPlan(
            environments.OrderBy(static value => value, StringComparer.OrdinalIgnoreCase).ToArray(),
            mergedEntries.Values
                .OrderBy(static entry => entry.Environment, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static entry => entry.Key, StringComparer.Ordinal)
                .ToArray(),
            warnings.Distinct(StringComparer.Ordinal).ToArray(),
            parameterCount);
    }

    private static PlannedConfigEntry ApplyRename(
        PlannedConfigEntry entry,
        IReadOnlyDictionary<string, string> renamedKeys)
    {
        var renameKey = BuildRenameKey(entry.Scope, entry.Key);
        return renamedKeys.TryGetValue(renameKey, out var renamedKey)
            ? entry with { Key = renamedKey }
            : entry;
    }

    private static string GetOrCreateRenamedKey(
        PlannedConfigEntry entry,
        IReadOnlyDictionary<string, PlannedConfigEntry> mergedEntries,
        IDictionary<string, string> renamedKeys)
    {
        var renameKey = BuildRenameKey(entry.Scope, entry.Key);
        if (renamedKeys.TryGetValue(renameKey, out var existingRenamedKey))
            return existingRenamedKey;

        var suffix = 1;
        string candidate;
        do
        {
            candidate = $"{entry.Key}_{suffix++}";
        }
        while (!IsRenameCandidateAvailable(candidate, mergedEntries, renamedKeys));

        renamedKeys[renameKey] = candidate;
        return candidate;
    }

    private static bool IsRenameCandidateAvailable(
        string candidate,
        IReadOnlyDictionary<string, PlannedConfigEntry> mergedEntries,
        IDictionary<string, string> renamedKeys)
    {
        if (renamedKeys.Values.Contains(candidate, StringComparer.OrdinalIgnoreCase))
            return false;

        return mergedEntries.Values.All(existing =>
            !string.Equals(existing.Key, candidate, StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildCompositeKey(string environment, string key)
    {
        return $"{environment}\n{key}";
    }

    private static string BuildRenameKey(string scope, string key)
    {
        return $"{NormalizeScope(scope)}\n{key}";
    }

    private static string MergeScopes(string left, string right)
    {
        var normalizedLeft = NormalizeScope(left);
        var normalizedRight = NormalizeScope(right);

        if (normalizedLeft == normalizedRight)
            return normalizedLeft;

        if (normalizedLeft == "all" || normalizedRight == "all")
            return "all";

        if ((normalizedLeft == "client" && normalizedRight == "server")
            || (normalizedLeft == "server" && normalizedRight == "client"))
            return "all";

        throw new InvalidOperationException($"Unsupported scope merge '{left}' + '{right}'.");
    }

    private static string NormalizeScope(string scope)
    {
        return scope.ToLowerInvariant() switch
        {
            "client" => "client",
            "server" => "server",
            "all" => "all",
            _ => throw new InvalidOperationException($"Invalid scope '{scope}'. Expected client/server/all.")
        };
    }
}
