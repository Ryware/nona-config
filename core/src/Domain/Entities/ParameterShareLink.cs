namespace Nona.Domain.Entities;

public class ParameterShareLink
{
    public long Id { get; set; }

    public required string TokenHash { get; set; }

    public required string Project { get; set; }

    public required string Environment { get; set; }

    public required string Key { get; set; }

    public bool CanEdit { get; set; }

    public required string CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime ExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }
}
