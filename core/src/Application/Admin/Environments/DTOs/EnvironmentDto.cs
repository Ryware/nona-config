namespace Nona.Application.Admin.Environments.DTOs;

public record EnvironmentDto(string Name, string Project, string? ActiveReleaseVersion, DateTime CreatedAt, DateTime UpdatedAt);
