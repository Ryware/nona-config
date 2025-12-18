namespace Nona.Domain.Entities;

public class ProjectEnvironment
{
    public required string Name { get; init; }

    public required string Project { get; init; }

    public List<string> ConfigEntries { get; init; } = new();

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
