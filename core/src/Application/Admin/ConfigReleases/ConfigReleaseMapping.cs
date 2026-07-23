using Nona.Application.Admin.ConfigReleases.DTOs;
using Nona.Application.Common;
using Nona.Domain.Entities;

namespace Nona.Application.Admin.ConfigReleases;

internal static class ConfigReleaseMapping
{
    public static ConfigReleaseDto ToDto(ConfigRelease release, string? activeReleaseVersion)
    {
        return new ConfigReleaseDto(
            release.Project,
            release.Environment,
            release.Version,
            release.EntryCount,
            IsActive(release, activeReleaseVersion),
            release.CreatedAt,
            release.Actor);
    }

    public static ConfigReleaseDetailsDto ToDetailsDto(ConfigRelease release, string? activeReleaseVersion)
    {
        return new ConfigReleaseDetailsDto(
            release.Project,
            release.Environment,
            release.Version,
            release.EntryCount,
            release.Entries.Select(ToEntryDto).ToList(),
            IsActive(release, activeReleaseVersion),
            release.CreatedAt,
            release.Actor);
    }

    private static ConfigReleaseEntryDto ToEntryDto(ConfigReleaseEntry entry)
    {
        return new ConfigReleaseEntryDto(
            entry.Key,
            entry.Value,
            entry.ContentType,
            entry.Scope.ToApiString());
    }

    private static bool IsActive(ConfigRelease release, string? activeReleaseVersion)
    {
        return activeReleaseVersion is not null
            && string.Equals(release.Version, activeReleaseVersion, StringComparison.OrdinalIgnoreCase);
    }
}
