using Nona.Application.Common;
using Nona.Domain.Entities;

namespace Nona.Application.Admin.ApiKeys.DTOs;

public record ApiKeyDto(
    long Id,
    string Name,
    string Fingerprint,
    string Project,
    string? Environment,
    string Scope,
    DateTime CreatedAt,
    DateTime UpdatedAt);

internal static class ApiKeyDtoMapping
{
    public static ApiKeyDto ToDto(this ApiKey apiKey)
    {
        return new ApiKeyDto(
            apiKey.Id,
            apiKey.Name,
            apiKey.Fingerprint,
            apiKey.Project,
            apiKey.Environment,
            apiKey.Scope.ToApiString(),
            apiKey.CreatedAt,
            apiKey.UpdatedAt);
    }

    public static CreatedApiKeyDto ToCreatedDto(this ApiKey apiKey, string secret)
    {
        return new CreatedApiKeyDto(
            apiKey.Id,
            apiKey.Name,
            secret,
            apiKey.Fingerprint,
            apiKey.Project,
            apiKey.Environment,
            apiKey.Scope.ToApiString(),
            apiKey.CreatedAt,
            apiKey.UpdatedAt);
    }
}

public record CreatedApiKeyDto(
    long Id,
    string Name,
    string Key,
    string Fingerprint,
    string Project,
    string? Environment,
    string Scope,
    DateTime CreatedAt,
    DateTime UpdatedAt);
