namespace Nona.Application.Admin.Projects.DTOs;

public record ProjectDto(string Name, string? ServerApiKey, string? ClientApiKey, List<string> Environments, DateTime CreatedAt, DateTime UpdatedAt);
