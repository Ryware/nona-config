namespace Nona.Migrator.Core.DTO;

public sealed class ConfigEntryDto
{
    public string Project { get; init; } = string.Empty;
    public string Environment { get; init; } = string.Empty;
    public string Key { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public string Scope { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
