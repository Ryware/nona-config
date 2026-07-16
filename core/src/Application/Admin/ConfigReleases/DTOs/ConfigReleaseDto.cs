namespace Nona.Application.Admin.ConfigReleases.DTOs;

public record ConfigReleaseDto(
    string Project,
    string Environment,
    string Version,
    int EntryCount,
    bool IsActive,
    DateTime CreatedAt,
    string Actor);
