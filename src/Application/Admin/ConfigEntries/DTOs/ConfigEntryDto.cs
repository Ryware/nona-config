namespace Nona.Application.Admin.ConfigEntries.DTOs;

public record ConfigEntryDto(string Project, string Environment, string Key, string Value, string ContentType, string Scope, DateTime CreatedAt, DateTime UpdatedAt);