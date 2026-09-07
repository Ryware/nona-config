using Mediator;
using Nona.Application.Admin.ConfigReleases;
using Nona.Application.Common;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Entities;
using Nona.Domain.Enums;
using Nona.Domain.Interfaces;

namespace Nona.Application.Api.ConfigEntries.Queries;

public record GetReleaseConfigEntryValueQuery(
    string EnvironmentId,
    string Key,
    string? Version = null) : IRequest<GetConfigEntryValueResult>;

public class GetReleaseConfigEntryValueQueryHandler(
    IApiKeyRepository apiKeyRepository,
    IEnvironmentRepository environmentRepository,
    IConfigReleaseRepository configReleaseRepository,
    IApiKeyService apiKeyService)
    : IRequestHandler<GetReleaseConfigEntryValueQuery, GetConfigEntryValueResult>
{
    public async ValueTask<GetConfigEntryValueResult> Handle(
        GetReleaseConfigEntryValueQuery request,
        CancellationToken cancellationToken)
    {
        var apiKeyHash = apiKeyService.GetCurrentApiKeyHash();
        if (string.IsNullOrEmpty(apiKeyHash))
            return Failure("API key is required", RuntimeConfigErrorCodes.InvalidApiKey);

        var lookupResult = await apiKeyRepository.GetByKeyHashAsync(apiKeyHash, cancellationToken);
        if (lookupResult is null)
            return Failure("Invalid API key", RuntimeConfigErrorCodes.InvalidApiKey);

        var (project, apiKeyScope, apiKeyEnvironment) = lookupResult;
        if (apiKeyEnvironment is not null &&
            !string.Equals(apiKeyEnvironment, request.EnvironmentId, StringComparison.OrdinalIgnoreCase))
        {
            return Failure("Environment not found", RuntimeConfigErrorCodes.EnvironmentNotFound);
        }

        var environment = await environmentRepository.GetAsync(project.Name, request.EnvironmentId, cancellationToken);
        if (environment is null)
            return Failure("Environment not found", RuntimeConfigErrorCodes.EnvironmentNotFound);

        ConfigReleaseEntryLookupResult releaseEntry;
        if (string.IsNullOrWhiteSpace(request.Version))
        {
            if (string.IsNullOrWhiteSpace(environment.ActiveReleaseVersion))
            {
                return Failure(
                    "The environment does not have an active release.",
                    RuntimeConfigErrorCodes.ActiveReleaseNotConfigured);
            }

            releaseEntry = await configReleaseRepository.GetEntryAsync(
                project.Name,
                environment.Name,
                environment.ActiveReleaseVersion,
                request.Key,
                apiKeyScope,
                cancellationToken);
        }
        else
        {
            if (!ConfigReleaseVersions.TryParseSelector(request.Version, out var selector))
            {
                return Failure(
                    "Version must use major.minor.patch or major.minor.x format.",
                    RuntimeConfigErrorCodes.InvalidReleaseVersion);
            }

            releaseEntry = selector.Kind == ConfigReleaseVersionKind.Line
                ? await configReleaseRepository.GetLatestPatchEntryAsync(
                    project.Name,
                    environment.Name,
                    selector.Major,
                    selector.Minor,
                    request.Key,
                    apiKeyScope,
                    cancellationToken)
                : await configReleaseRepository.GetEntryAsync(
                    project.Name,
                    environment.Name,
                    selector.Normalized,
                    request.Key,
                    apiKeyScope,
                    cancellationToken);
        }

        if (!releaseEntry.ReleaseFound)
            return Failure("Release not found", RuntimeConfigErrorCodes.ReleaseNotFound);
        if (releaseEntry.Entry is null)
            return Failure("Config entry not found", RuntimeConfigErrorCodes.ConfigEntryNotFound);

        return new GetConfigEntryValueResult(
            true,
            releaseEntry.Entry.Value,
            ConfigEntryContentTypes.Normalize(releaseEntry.Entry.ContentType)
                ?? ConfigEntryContentTypes.Infer(releaseEntry.Entry.Value),
            null);
    }

    private static GetConfigEntryValueResult Failure(string error, string errorCode) =>
        new(false, null, null, error, errorCode);
}
