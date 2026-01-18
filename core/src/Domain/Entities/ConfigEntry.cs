using Nona.Domain.Enums;

namespace Nona.Domain.Entities;

public class ConfigEntry
{
    public required string Project { get; init; }
    public required string Environment { get; init; }
    public required string Key { get; init; }


    public required string Value { get; set; }
    public string ContentType { get; set; } = "string";

    public KeyScope Scope { get; set; } = KeyScope.All;

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
