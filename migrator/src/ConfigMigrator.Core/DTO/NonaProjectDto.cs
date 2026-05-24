namespace Nona.Migrator.Core.DTO;

public sealed class NonaProjectDto
{
    public long Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? UrlSlug { get; init; }
    public string? ServerApiKey { get; init; }
    public string? ClientApiKey { get; init; }
    public IReadOnlyList<string> Environments { get; init; } = [];
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
