using Nona.Domain.Enums;

namespace Nona.Domain.Entities;

public class ConfigEntry
{
    public required string Project { get; init; }
    public required string Environment { get; init; }
    public required string Key { get; init; }

    public required string Value { get; set; }
    public string ContentType { get; set; } = "text";

    public KeyScope Scope { get; set; } = KeyScope.All;

    public int ActiveVersion { get; set; } = 1;

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
