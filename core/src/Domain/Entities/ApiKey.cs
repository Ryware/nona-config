using Nona.Domain.Enums;

namespace Nona.Domain.Entities;

public class ApiKey
{
    public long Id { get; set; }
    public required string Name { get; set; }
    public required string Key { get; set; }
    public required string Project { get; set; }
    public string? Environment { get; set; }
    public KeyScope Scope { get; set; } = KeyScope.Frontend;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
