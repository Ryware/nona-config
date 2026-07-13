namespace Nona.Application.Admin.Projects.DTOs;

public record ProjectDto(long Id, string Name, string? UrlSlug, List<string> Environments, DateTime CreatedAt, DateTime UpdatedAt);
