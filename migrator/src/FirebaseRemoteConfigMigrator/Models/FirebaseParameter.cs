namespace Nona.FirebaseRemoteConfigMigrator.Models;

public sealed class FirebaseParameter
{
    public string? ValueType { get; init; }
    public FirebaseValue? DefaultValue { get; init; }
    public IReadOnlyDictionary<string, FirebaseValue>? ConditionalValues { get; init; } =
        new Dictionary<string, FirebaseValue>(StringComparer.OrdinalIgnoreCase);
}
