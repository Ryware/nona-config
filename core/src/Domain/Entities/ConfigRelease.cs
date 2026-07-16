namespace Nona.Domain.Entities;

public class ConfigRelease
{
    public required string Project { get; init; }
    public required string Environment { get; init; }
    public required string Version { get; init; }
    public int Major { get; init; }
    public int Minor { get; init; }
    public int Patch { get; init; }
    public IReadOnlyList<ConfigReleaseEntry> Entries { get; init; } = [];
    public int EntryCount { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public string Actor { get; init; } = "System";
}
