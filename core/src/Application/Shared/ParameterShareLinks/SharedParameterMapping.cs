using Nona.Application.Shared.ParameterShareLinks.DTOs;
using Nona.Domain.Entities;

namespace Nona.Application.Shared.ParameterShareLinks;

internal static class SharedParameterMapping
{
    public static string ResolveActor(ParameterShareLink shareLink)
        => $"Shared link #{shareLink.Id}";

    public static SharedParameterDto ToDto(ConfigEntry entry, bool canEdit, DateTime expiresAt)
    {
        return new SharedParameterDto(
            entry.Environment,
            entry.Key,
            entry.Value,
            entry.ContentType,
            canEdit,
            expiresAt,
            entry.Unit);
    }
}
