using Mediator;
using Nona.Application.Admin.ConfigReleases;
using Nona.Application.Common;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Entities;
using Nona.Domain.Enums;
using Nona.Domain.Interfaces;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Nona.Application.Api.ConfigEntries.Queries;

public record GetAllConfigValuesQuery(
    string EnvironmentId,
    string? Version = null,
    string? IfNoneMatch = null)
    : IRequest<GetAllConfigValuesResult>;

public record ClientConfigValueDto(string Value, string ContentType);

public record GetAllConfigValuesResult(
    bool Success,
    Dictionary<string, ClientConfigValueDto>? Values,
    string? Error,
    string? Etag = null,
    bool NotModified = false);

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
            environment.Name,
            environment.ActiveReleaseVersion,
            request.Version,
            cancellationToken);
        if (release.Error is not null)
            return Failure(release.Error);

        var resolvedRelease = release.Release!;
        var etag = CreateReleaseEtag(project.Name, environment.Name, resolvedRelease);
        if (MatchesIfNoneMatch(request.IfNoneMatch, etag))
            return new GetAllConfigValuesResult(true, null, null, etag, true);

        var entries = await configReleaseRepository.ListEntriesAsync(
            project.Name,
            environment.Name,
            resolvedRelease.Version,
            KeyScope.Frontend,
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

            var activeRelease = await configReleaseRepository.GetMetadataAsync(
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
            ? await configReleaseRepository.GetLatestPatchMetadataAsync(
                projectName,
                environmentName,
                version.Major,
                version.Minor,
                cancellationToken)
            : await configReleaseRepository.GetMetadataAsync(
                projectName,
                environmentName,
                version.Normalized,
                cancellationToken);

        return release is null
            ? (null, "Release not found")
            : (release, null);
    }

    private static string CreateReleaseEtag(
        string projectName,
        string environmentName,
        ConfigRelease release)
    {
        var canonical = new StringBuilder("client-config-v1");
        AppendEtagPart(canonical, projectName);
        AppendEtagPart(canonical, environmentName);
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

    private static bool MatchesIfNoneMatch(string? headerValue, string etag)
    {
        if (string.IsNullOrWhiteSpace(headerValue))
            return false;

        foreach (var rawCandidate in headerValue.Split(
                     ',',
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (rawCandidate == "*")
                return true;

            var candidate = rawCandidate.StartsWith("W/", StringComparison.OrdinalIgnoreCase)
                ? rawCandidate[2..].TrimStart()
                : rawCandidate;

            if (string.Equals(candidate, etag, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static GetAllConfigValuesResult Failure(string error) =>
        new(false, null, error);
}
