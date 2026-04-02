namespace Nona.Domain.Entities;

public class ProjectMember
{
    public required string Username { get; init; }
    public required string ProjectId { get; init; }
    public required ProjectRole Role { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

public enum ProjectRole
{
    Viewer,
    Editor
}
