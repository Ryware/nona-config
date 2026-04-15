namespace Nona.FirebaseRemoteConfigMigrator.Models;

internal sealed record PlannedConfigEntry(
    string Environment,
    string Key,
    string Value,
    string ContentType,
    string Scope,
    string SourceLabel);
