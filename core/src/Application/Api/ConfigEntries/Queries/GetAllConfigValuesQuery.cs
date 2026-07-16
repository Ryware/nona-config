using Mediator;
using Nona.Application.Admin.ConfigReleases;
using Nona.Application.Common;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Entities;
using Nona.Domain.Enums;
using Nona.Domain.Interfaces;

namespace Nona.Application.Api.ConfigEntries.Queries;

public record GetAllConfigValuesQuery(string EnvironmentId, string? Version = null)
    : IRequest<GetAllConfigValuesResult>;

public record ClientConfigValueDto(string Value, string ContentType);

public record GetAllConfigValuesResult(
    bool Success,
    Dictionary<string, ClientConfigValueDto>? Values,
    string? Error);

public class GetAllConfigValuesQueryHandler(
    IApiKeyRepository apiKeyRepository,
    IEnvironmentRepository environmentRepository,
    IConfigReleaseRepository configReleaseRepository,
    IApiKeyService apiKeyService)
    : IRequestHandler<GetAllConfigValuesQuery, GetAllConfigValuesResult>
{
    public async ValueTask<GetAllConfigValuesResult> Handle(
        GetAllConfigValuesQuery request,
        CancellationToken cancellationToken)
    {
        var apiKey = apiKeyService.GetCurrentApiKey();
        if (string.IsNullOrEmpty(apiKey))
            return Failure("API key is required");

        var lookupResult = await apiKeyRepository.GetByKeyAsync(apiKey, cancellationToken);
        if (lookupResult is null)
            return Failure("Invalid API key");

        var (project, apiKeyScope, apiKeyEnvironment) = lookupResult;

        // This endpoint is intentionally a client-facing snapshot. A backend-only
        // key must not be able to use it to enumerate an environment.
        if ((apiKeyScope & KeyScope.Frontend) == 0)
            return Failure("Environment not found");

        if (apiKeyEnvironment is not null &&
            !string.Equals(apiKeyEnvironment, request.EnvironmentId, StringComparison.OrdinalIgnoreCase))
        {
            return Failure("Environment not found");
        }

        var environment = await environmentRepository.GetAsync(
            project.Name,
            request.EnvironmentId,
            cancellationToken);
        if (environment is null)
            return Failure("Environment not found");

        var release = await ResolveReleaseAsync(
            project.Name,
            request.EnvironmentId,
            environment.ActiveReleaseVersion,
            request.Version,
            cancellationToken);
        if (release.Error is not null)
            return Failure(release.Error);

        var values = release.Release!.Entries
            .Where(entry => (entry.Scope & KeyScope.Frontend) != 0)
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .ToDictionary(
                entry => entry.Key,
                entry => new ClientConfigValueDto(
                    entry.Value,
                    ConfigEntryContentTypes.Normalize(entry.ContentType)
                        ?? ConfigEntryContentTypes.Infer(entry.Value)),
                StringComparer.Ordinal);

        return new GetAllConfigValuesResult(true, values, null);
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
                return (null, "Active release not configured");

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
            return (null, "Version must use major.minor.patch or major.minor.x format.");

        var release = version.Kind == ConfigReleaseVersionKind.Line
            ? await configReleaseRepository.GetLatestPatchAsync(
                projectName,
                environmentName,
                version.Major,
                version.Minor,
                cancellationToken)
            : await configReleaseRepository.GetAsync(
                projectName,
                environmentName,
                version.Normalized,
                cancellationToken);

        return release is null
            ? (null, "Release not found")
            : (release, null);
    }

    private static GetAllConfigValuesResult Failure(string error) =>
        new(false, null, error);
}
