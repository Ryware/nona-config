namespace Nona.Domain.Entities;

public class Project
{
    public required string Name { get; init; }
    public string? ServerApiKey { get; set; }
    public string? ClientApiKey { get; set; }
    public List<string> Environments { get; init; } = new();

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
