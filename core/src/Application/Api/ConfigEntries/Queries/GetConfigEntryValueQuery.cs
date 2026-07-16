using Mediator;
using Nona.Application.Admin.ConfigReleases;
using Nona.Application.Common;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Entities;
using Nona.Domain.Interfaces;

namespace Nona.Application.Api.ConfigEntries.Queries;

public record GetConfigEntryValueQuery(string EnvironmentId, string Key, string? Version = null) : IRequest<GetConfigEntryValueResult>;

public record GetConfigEntryValueResult(bool Success, string? Value, string? LogicalContentType, string? Error);

public class GetConfigEntryValueQueryHandler(
    IApiKeyRepository apiKeyRepository,
    IEnvironmentRepository environmentRepository,
    IConfigReleaseRepository configReleaseRepository,
    IApiKeyService apiKeyService)
    : IRequestHandler<GetConfigEntryValueQuery, GetConfigEntryValueResult>
{
    public async ValueTask<GetConfigEntryValueResult> Handle(GetConfigEntryValueQuery request, CancellationToken cancellationToken)
    {
        var apiKey = apiKeyService.GetCurrentApiKey();
        if (string.IsNullOrEmpty(apiKey))
            return new GetConfigEntryValueResult(false, null, null, "API key is required");

        var lookupResult = await apiKeyRepository.GetByKeyAsync(apiKey, cancellationToken);
        if (lookupResult is null)
            return new GetConfigEntryValueResult(false, null, null, "Invalid API key");

        var (project, apiKeyScope, apiKeyEnvironment) = lookupResult;

        if (apiKeyEnvironment is not null &&
            !string.Equals(apiKeyEnvironment, request.EnvironmentId, StringComparison.OrdinalIgnoreCase))
        {
            return new GetConfigEntryValueResult(false, null, null, "Environment not found");
        }

        var environment = await environmentRepository.GetAsync(project.Name, request.EnvironmentId, cancellationToken);
        if (environment is null)
            return new GetConfigEntryValueResult(false, null, null, "Environment not found");

        var release = await ResolveReleaseAsync(project.Name, request.EnvironmentId, environment.ActiveReleaseVersion, request.Version, cancellationToken);
        if (release.Error is not null)
            return new GetConfigEntryValueResult(false, null, null, release.Error);

        var configEntry = release.Release!.Entries.FirstOrDefault(entry =>
            entry.Key.Equals(request.Key, StringComparison.OrdinalIgnoreCase));
        if (configEntry is null)
            return new GetConfigEntryValueResult(false, null, null, "Config entry not found");

        if ((apiKeyScope & configEntry.Scope) == 0)
            return new GetConfigEntryValueResult(false, null, null, "Config entry not found");

        var contentType = ConfigEntryContentTypes.Normalize(configEntry.ContentType)
            ?? ConfigEntryContentTypes.Infer(configEntry.Value);

        return new GetConfigEntryValueResult(true, configEntry.Value, contentType, null);
    }

    private async Task<(ConfigRelease? Release, string? Error)> ResolveReleaseAsync(
        string projectName,
        string environmentName,
        string? activeReleaseVersion,
        string? requestedVersion,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(requestedVersion))
        {
            if (string.IsNullOrWhiteSpace(activeReleaseVersion))
            {
                return (null, "Active release not configured");
            }

            var activeRelease = await configReleaseRepository.GetAsync(
                projectName,
                environmentName,
                activeReleaseVersion,
                cancellationToken);

            return activeRelease is null
                ? (null, "Release not found")
                : (activeRelease, null);
        }

        if (!ConfigReleaseVersions.TryParseSelector(requestedVersion, out var version))
        {
            return (null, "Version must use major.minor.patch or major.minor.x format.");
        }

        var release = version.Kind == ConfigReleaseVersionKind.Line
            ? await configReleaseRepository.GetLatestPatchAsync(projectName, environmentName, version.Major, version.Minor, cancellationToken)
            : await configReleaseRepository.GetAsync(projectName, environmentName, version.Normalized, cancellationToken);

        return release is null
            ? (null, "Release not found")
            : (release, null);
    }
}
