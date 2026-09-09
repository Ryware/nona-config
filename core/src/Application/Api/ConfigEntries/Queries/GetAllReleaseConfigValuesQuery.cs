using Mediator;
using Nona.Application.Admin.ConfigReleases;
using Nona.Application.Common;
using Nona.Application.Common.Interfaces;
using Nona.Domain;
using Nona.Domain.Entities;
using Nona.Domain.Enums;
using Nona.Domain.Interfaces;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Nona.Application.Api.ConfigEntries.Queries;

public record GetAllReleaseConfigValuesQuery(
    string EnvironmentId,
    string? Version = null,
    string? Prefix = null,
    string? IfNoneMatch = null)
    : IRequest<GetAllConfigValuesResult>;

public class GetAllReleaseConfigValuesQueryHandler(
    IApiKeyRepository apiKeyRepository,
    IEnvironmentRepository environmentRepository,
    IConfigReleaseRepository configReleaseRepository,
    IApiKeyService apiKeyService)
    : IRequestHandler<GetAllReleaseConfigValuesQuery, GetAllConfigValuesResult>
{
    public async ValueTask<GetAllConfigValuesResult> Handle(
        GetAllReleaseConfigValuesQuery request,
        CancellationToken cancellationToken)
    {
        if (!ConfigEntryPrefix.IsValid(request.Prefix))
            return Failure(ConfigEntryPrefix.ValidationError, RuntimeConfigErrorCodes.InvalidPrefix);

        var apiKeyHash = apiKeyService.GetCurrentApiKeyHash();
        if (string.IsNullOrEmpty(apiKeyHash))
            return Failure("API key is required", RuntimeConfigErrorCodes.InvalidApiKey);

        var lookupResult = await apiKeyRepository.GetByKeyHashAsync(apiKeyHash, cancellationToken);
        if (lookupResult is null)
            return Failure("Invalid API key", RuntimeConfigErrorCodes.InvalidApiKey);

        var (project, apiKeyScope, apiKeyEnvironment) = lookupResult;
        if ((apiKeyScope & KeyScope.Frontend) == 0)
            return Failure("Environment not found", RuntimeConfigErrorCodes.EnvironmentNotFound);
        if (apiKeyEnvironment is not null &&
            !string.Equals(apiKeyEnvironment, request.EnvironmentId, StringComparison.OrdinalIgnoreCase))
        {
            return Failure("Environment not found", RuntimeConfigErrorCodes.EnvironmentNotFound);
        }

        var environment = await environmentRepository.GetAsync(
            project.Name,
            request.EnvironmentId,
            cancellationToken);
        if (environment is null)
            return Failure("Environment not found", RuntimeConfigErrorCodes.EnvironmentNotFound);

        ConfigRelease? release;
        if (string.IsNullOrWhiteSpace(request.Version))
        {
            if (string.IsNullOrWhiteSpace(environment.ActiveReleaseVersion))
            {
                return Failure(
                    "The environment does not have an active release.",
                    RuntimeConfigErrorCodes.ActiveReleaseNotConfigured);
            }

            release = await configReleaseRepository.GetMetadataAsync(
                project.Name,
                environment.Name,
                environment.ActiveReleaseVersion,
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

            release = selector.Kind == ConfigReleaseVersionKind.Line
                ? await configReleaseRepository.GetLatestPatchMetadataAsync(
                    project.Name,
                    environment.Name,
                    selector.Major,
                    selector.Minor,
                    cancellationToken)
                : await configReleaseRepository.GetMetadataAsync(
                    project.Name,
                    environment.Name,
                    selector.Normalized,
                    cancellationToken);
        }

        if (release is null)
            return Failure("Release not found", RuntimeConfigErrorCodes.ReleaseNotFound);

        var etag = CreateReleaseEtag(
            project.Name,
            environment.Name,
            ConfigEntryPrefix.Normalize(request.Prefix),
            release);
        if (GetAllConfigValuesQueryHandler.MatchesIfNoneMatch(request.IfNoneMatch, etag))
            return new GetAllConfigValuesResult(true, null, null, etag, true);

        var entries = string.IsNullOrEmpty(request.Prefix)
            ? await configReleaseRepository.ListEntriesAsync(
                project.Name,
                environment.Name,
                release.Version,
                KeyScope.Frontend,
                cancellationToken)
            : await configReleaseRepository.ListEntriesAsync(
                project.Name,
                environment.Name,
                release.Version,
                KeyScope.Frontend,
                request.Prefix,
                cancellationToken);
        var values = entries
            .Where(entry => (entry.Scope & KeyScope.Frontend) != 0)
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .ToDictionary(
                entry => entry.Key,
                entry => new ClientConfigValueDto(
                    entry.Value,
                    ConfigEntryContentTypes.Normalize(entry.ContentType)
                        ?? ConfigEntryContentTypes.Infer(entry.Value)),
                StringComparer.Ordinal);

        return new GetAllConfigValuesResult(true, values, null, etag);
    }

    private static string CreateReleaseEtag(
        string projectName,
        string environmentName,
        string? normalizedPrefix,
        ConfigRelease release)
    {
        var canonical = new StringBuilder("client-config-release-v1");
        AppendEtagPart(canonical, projectName);
        AppendEtagPart(canonical, environmentName);
        if (normalizedPrefix is not null)
        {
            AppendEtagPart(canonical, "prefix-v2");
            AppendEtagPart(canonical, normalizedPrefix);
        }

        AppendEtagPart(canonical, release.Version);
        AppendEtagPart(
            canonical,
            release.CreatedAt.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture));
        AppendEtagPart(canonical, release.EntryCount.ToString(CultureInfo.InvariantCulture));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()));
        return $"\"{Convert.ToHexString(hash).ToLowerInvariant()}\"";
    }

    private static void AppendEtagPart(StringBuilder builder, string value)
    {
        builder.Append(value.Length).Append(':').Append(value);
    }

    private static GetAllConfigValuesResult Failure(string error, string errorCode) =>
        new(false, null, error, ErrorCode: errorCode);
}
