using Nona.Domain.Enums;

namespace Nona.Domain.Entities;

public class ConfigEntryVersion
{
    public required string Project { get; init; }
    public required string Environment { get; init; }
    public required string Key { get; init; }
    public int Version { get; init; }
    public required string Value { get; init; }
    public string ContentType { get; init; } = "text";
    public KeyScope Scope { get; init; } = KeyScope.All;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public string Actor { get; init; } = "System";
}
