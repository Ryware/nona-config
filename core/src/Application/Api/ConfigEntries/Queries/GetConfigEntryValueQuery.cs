using Mediator;
using Nona.Application.Admin.ConfigReleases;
using Nona.Application.Common;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Entities;
using Nona.Domain.Enums;
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

        var lookup = await ResolveEntryAsync(
            project.Name,
            request.EnvironmentId,
            environment.ActiveReleaseVersion,
            request.Version,
            request.Key,
            apiKeyScope,
            cancellationToken);
        if (lookup.Error is not null)
            return new GetConfigEntryValueResult(false, null, null, lookup.Error);

        var configEntry = lookup.Result!.Entry;
        if (configEntry is null)
            return new GetConfigEntryValueResult(false, null, null, "Config entry not found");

        var contentType = ConfigEntryContentTypes.Normalize(configEntry.ContentType)
            ?? ConfigEntryContentTypes.Infer(configEntry.Value);

        return new GetConfigEntryValueResult(true, configEntry.Value, contentType, null);
    }

    private async Task<(ConfigReleaseEntryLookupResult? Result, string? Error)> ResolveEntryAsync(
        string projectName,
        string environmentName,
        string? activeReleaseVersion,
        string? requestedVersion,
        string key,
        KeyScope requiredScope,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(requestedVersion))
        {
            if (string.IsNullOrWhiteSpace(activeReleaseVersion))
            {
                return (null, "Active release not configured");
            }

            var activeReleaseEntry = await configReleaseRepository.GetEntryAsync(
                projectName,
                environmentName,
                activeReleaseVersion,
                key,
                requiredScope,
                cancellationToken);

            return !activeReleaseEntry.ReleaseFound
                ? (null, "Release not found")
                : (activeReleaseEntry, null);
        }

        if (!ConfigReleaseVersions.TryParseSelector(requestedVersion, out var version))
        {
            return (null, "Version must use major.minor.patch or major.minor.x format.");
        }

        var releaseEntry = version.Kind == ConfigReleaseVersionKind.Line
            ? await configReleaseRepository.GetLatestPatchEntryAsync(
                projectName,
                environmentName,
                version.Major,
                version.Minor,
                key,
                requiredScope,
                cancellationToken)
            : await configReleaseRepository.GetEntryAsync(
                projectName,
                environmentName,
                version.Normalized,
                key,
                requiredScope,
                cancellationToken);

        return !releaseEntry.ReleaseFound
            ? (null, "Release not found")
            : (releaseEntry, null);
    }
}
