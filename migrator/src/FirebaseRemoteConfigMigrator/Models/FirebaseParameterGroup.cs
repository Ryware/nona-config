namespace Nona.FirebaseRemoteConfigMigrator.Models;

public sealed class FirebaseParameterGroup
{
    public IReadOnlyDictionary<string, FirebaseParameter>? Parameters { get; init; } =
        new Dictionary<string, FirebaseParameter>(StringComparer.Ordinal);
}
