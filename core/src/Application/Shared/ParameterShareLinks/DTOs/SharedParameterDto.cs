namespace Nona.Application.Shared.ParameterShareLinks.DTOs;

public record SharedParameterDto(
    string Environment,
    string Key,
    string Value,
    string ContentType,
    bool CanEdit,
    DateTime ExpiresAt);
