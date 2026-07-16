namespace Nona.Application.Admin.ConfigReleases.DTOs;

public record ConfigReleaseDetailsDto(
    string Project,
    string Environment,
    string Version,
    int EntryCount,
    IReadOnlyList<ConfigReleaseEntryDto> Entries,
    bool IsActive,
    DateTime CreatedAt,
    string Actor);
