using Microsoft.Kiota.Abstractions;
using Nona.Cli.Generated.Models;

namespace Nona.Cli.Entries;

internal sealed record AdminReleaseEntryReadResult(
    bool Success,
    List<ConfigReleaseEntryDto>? Entries,
    string? ResolvedVersion,
    string? Error);

internal static class AdminReleaseEntryReader
{
    internal static async Task<AdminReleaseEntryReadResult> ReadAsync(
        OwnedNonaApiClient api,
        string project,
        string environment,
        string? releaseVersion,
        CancellationToken ct)
    {
        var version = releaseVersion;
        if (string.IsNullOrWhiteSpace(version))
        {
            var environments = await api.Admin.Projects[project].Environments.GetAsync(cancellationToken: ct) ?? [];
            var match = environments.FirstOrDefault(e =>
                string.Equals(e.Name, environment, StringComparison.OrdinalIgnoreCase));

            version = match?.ActiveReleaseVersion;
            if (string.IsNullOrWhiteSpace(version))
                return new AdminReleaseEntryReadResult(false, null, null, $"No active release is configured for [{environment}].");
        }

        try
        {
            var release = await api.Admin.Projects[project]
                .Environments[environment].Releases[version]
                .GetAsync(cancellationToken: ct);
            return new AdminReleaseEntryReadResult(true, release?.Entries ?? [], version, null);
        }
        catch (ApiException ex) when (ex.ResponseStatusCode == 404)
        {
            return new AdminReleaseEntryReadResult(false, null, version, $"Release '{version}' not found in [{environment}].");
        }
    }

    internal static ConfigEntryDto ToConfigEntryDto(this ConfigReleaseEntryDto source, string project, string environment)
        => new()
        {
            Project = project,
            Environment = environment,
            Key = source.Key,
            Value = source.Value,
            ContentType = source.ContentType,
            Scope = source.Scope,
            Description = source.Description,
            Unit = source.Unit
        };
}
