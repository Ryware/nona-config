namespace Nona.FirebaseRemoteConfigMigrator.Models;

internal sealed record FirebaseImportSource
{
    public string? Namespace { get; init; }
    public string Scope { get; init; } = "all";

    public string DisplayName => string.IsNullOrWhiteSpace(Namespace) ? "<default>" : Namespace;
}
