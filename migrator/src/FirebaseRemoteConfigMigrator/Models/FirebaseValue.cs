namespace Nona.FirebaseRemoteConfigMigrator.Models;

public sealed class FirebaseValue
{
    public string? Value { get; init; }
    public bool? UseInAppDefault { get; init; }

    public bool TryGetExplicitValue(out string value)
    {
        if (Value is not null)
        {
            value = Value;
            return true;
        }

        value = string.Empty;
        return false;
    }
}
