namespace Nona.Migrator.Core.Models;

public sealed record MigrationPlan(
    IReadOnlyList<string> Environments,
    IReadOnlyList<PlannedConfigEntry> Entries,
    IReadOnlyList<string> Warnings,
    int ParameterCount);
