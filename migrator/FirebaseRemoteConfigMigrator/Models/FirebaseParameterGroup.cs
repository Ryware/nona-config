namespace Nona.FirebaseRemoteConfigMigrator.Models;

internal sealed class FirebaseParameterGroup
{
    public IReadOnlyDictionary<string, FirebaseParameter>? Parameters { get; init; } =
        new Dictionary<string, FirebaseParameter>(StringComparer.Ordinal);
}
