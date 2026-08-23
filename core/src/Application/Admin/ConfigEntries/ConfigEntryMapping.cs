using Nona.Application.Admin.ConfigEntries.DTOs;
using Nona.Application.Common;
using Nona.Domain.Entities;

namespace Nona.Application.Admin.ConfigEntries;

internal static class ConfigEntryMapping
{
    public static ConfigEntryDto ToDto(ConfigEntry entry)
    {
        return new ConfigEntryDto(
            entry.Project,
            entry.Environment,
            entry.Key,
            entry.Value,
            entry.ContentType,
            entry.Scope.ToApiString(),
            entry.ActiveVersion,
            entry.CreatedAt,
            entry.UpdatedAt,
            entry.Description,
            entry.Unit);
    }

    public static ConfigEntryVersionDto ToDto(ConfigEntryVersion version)
    {
        return new ConfigEntryVersionDto(
            version.Project,
            version.Environment,
            version.Key,
            version.Version,
            version.Value,
            version.ContentType,
            version.Scope.ToApiString(),
            version.CreatedAt,
            version.Actor,
            version.Description,
            version.Unit);
    }
}
