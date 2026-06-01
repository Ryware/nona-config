using Nona.Application.Common;
using Nona.Domain.Entities;

namespace Nona.Application.Admin.ApiKeys.DTOs;

public record ApiKeyDto(
    long Id,
    string Name,
    string Key,
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
            apiKey.Key,
            apiKey.Project,
            apiKey.Environment,
            apiKey.Scope.ToApiString(),
            apiKey.CreatedAt,
            apiKey.UpdatedAt);
    }
}
