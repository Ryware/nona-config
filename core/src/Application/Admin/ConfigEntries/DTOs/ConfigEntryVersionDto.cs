namespace Nona.Application.Admin.ConfigEntries.DTOs;

public record ConfigEntryVersionDto(
    string Project,
    string Environment,
    string Key,
    int Version,
    string Value,
    string ContentType,
    string Scope,
    DateTime CreatedAt,
    string Actor);
