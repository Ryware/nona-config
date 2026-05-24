using Nona.FirebaseRemoteConfigMigrator.Models;

namespace Nona.Migrator.FirebaseRemoteConfig.Service;

internal sealed class FirebaseRemoteConfigTemplate
{
    public IReadOnlyList<FirebaseCondition>? Conditions { get; init; } = [];
    public IReadOnlyDictionary<string, FirebaseParameter>? Parameters { get; init; } =
        new Dictionary<string, FirebaseParameter>(StringComparer.Ordinal);
    public IReadOnlyDictionary<string, FirebaseParameterGroup>? ParameterGroups { get; init; } =
        new Dictionary<string, FirebaseParameterGroup>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, FirebaseParameter> GetAllParameters(ICollection<string> warnings)
    {
        var flattened = new Dictionary<string, FirebaseParameter>(StringComparer.Ordinal);

        foreach (var (key, parameter) in Parameters ?? new Dictionary<string, FirebaseParameter>(StringComparer.Ordinal))
            flattened.Add(key, parameter);

        foreach (var (groupName, group) in ParameterGroups ?? new Dictionary<string, FirebaseParameterGroup>(StringComparer.Ordinal))
        {
            foreach (var (key, parameter) in group.Parameters ?? new Dictionary<string, FirebaseParameter>(StringComparer.Ordinal))
            {
                if (!flattened.TryAdd(key, parameter))
                    throw new InvalidOperationException($"Duplicate Firebase key '{key}' found while flattening group '{groupName}'.");
            }
        }

        if (flattened.Count == 0)
            warnings.Add("Firebase template has no parameters.");

        return flattened;
    }
}
