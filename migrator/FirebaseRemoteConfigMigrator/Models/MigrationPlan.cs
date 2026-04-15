namespace Nona.FirebaseRemoteConfigMigrator.Models;

internal sealed record MigrationPlan(
    IReadOnlyList<string> Environments,
    IReadOnlyList<PlannedConfigEntry> Entries,
    IReadOnlyList<string> Warnings,
    int ParameterCount);
