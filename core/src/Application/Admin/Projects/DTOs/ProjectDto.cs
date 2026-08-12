namespace Nona.Application.Admin.Projects.DTOs;

public record ProjectDto(long Id, string Name, string? UrlSlug, string AccessLevel, List<string> Environments, DateTime CreatedAt, DateTime UpdatedAt);
