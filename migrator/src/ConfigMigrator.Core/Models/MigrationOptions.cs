namespace Nona.Migrator.Core.Models;

public sealed record MigrationOptions
{
    public bool DryRun { get; init; }
    public bool RenameConflictingKeys { get; init; }
    public bool ApplyDefaultToMappedEnvironments { get; init; } = true;
    public IReadOnlyList<string> DefaultValueEnvironments { get; init; } = [];
    public IReadOnlyDictionary<string, string> ConditionEnvironmentMappings { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
