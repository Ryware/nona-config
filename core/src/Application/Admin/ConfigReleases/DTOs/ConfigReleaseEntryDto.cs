namespace Nona.Application.Admin.ConfigReleases.DTOs;

public record ConfigReleaseEntryDto(
    string Key,
    string Value,
    string ContentType,
    string Scope);
