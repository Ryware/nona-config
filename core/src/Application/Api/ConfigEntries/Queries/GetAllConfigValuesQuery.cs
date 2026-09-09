using Mediator;
using Nona.Application.Common;
using Nona.Application.Common.Interfaces;
using Nona.Domain;
using Nona.Domain.Enums;
using Nona.Domain.Interfaces;
using System.Security.Cryptography;
using System.Text;

namespace Nona.Application.Api.ConfigEntries.Queries;

public record GetAllConfigValuesQuery(
    string EnvironmentId,
    string? Prefix = null,
    string? IfNoneMatch = null)
    : IRequest<GetAllConfigValuesResult>;

public record ClientConfigValueDto(string Value, string ContentType);

public record GetAllConfigValuesResult(
    bool Success,
    Dictionary<string, ClientConfigValueDto>? Values,
    string? Error,
    string? Etag = null,
    bool NotModified = false,
    string? ErrorCode = null);

public class GetAllConfigValuesQueryHandler(
    IApiKeyRepository apiKeyRepository,
    IEnvironmentRepository environmentRepository,
    IConfigEntryRepository configEntryRepository,
    IApiKeyService apiKeyService)
    : IRequestHandler<GetAllConfigValuesQuery, GetAllConfigValuesResult>
{
    public async ValueTask<GetAllConfigValuesResult> Handle(
        GetAllConfigValuesQuery request,
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

        // This endpoint is intentionally a client-facing snapshot. A backend-only
        // key must not be able to use it to enumerate an environment.
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

        var normalizedPrefix = ConfigEntryPrefix.Normalize(request.Prefix);

        var workingEntries = string.IsNullOrEmpty(request.Prefix)
            ? await configEntryRepository.ListAsync(
                project.Name,
                environment.Name,
                cancellationToken)
            : await configEntryRepository.ListAsync(
                project.Name,
                environment.Name,
                request.Prefix,
                cancellationToken);
        var workingValues = workingEntries
            .Where(entry => (entry.Scope & KeyScope.Frontend) != 0)
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .ToDictionary(
                entry => entry.Key,
                entry => new ClientConfigValueDto(
                    entry.Value,
                    ConfigEntryContentTypes.Normalize(entry.ContentType)
                        ?? ConfigEntryContentTypes.Infer(entry.Value)),
                StringComparer.Ordinal);

        var workingEtag = CreateWorkingConfigEtag(
            project.Name,
            environment.Name,
            normalizedPrefix,
            workingValues);

        return MatchesIfNoneMatch(request.IfNoneMatch, workingEtag)
            ? new GetAllConfigValuesResult(true, null, null, workingEtag, true)
            : new GetAllConfigValuesResult(true, workingValues, null, workingEtag);
    }

    private static string CreateWorkingConfigEtag(
        string projectName,
        string environmentName,
        string? normalizedPrefix,
        IReadOnlyDictionary<string, ClientConfigValueDto> values)
    {
        var canonical = new StringBuilder("client-config-working-v1");
        AppendEtagPart(canonical, projectName);
        AppendEtagPart(canonical, environmentName);
        AppendPrefixEtagPart(canonical, normalizedPrefix);
        foreach (var pair in values.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            AppendEtagPart(canonical, pair.Key);
            AppendEtagPart(canonical, pair.Value.Value);
            AppendEtagPart(canonical, pair.Value.ContentType);
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()));
        return $"\"{Convert.ToHexString(hash).ToLowerInvariant()}\"";
    }

    private static void AppendEtagPart(StringBuilder builder, string value)
    {
        builder.Append(value.Length).Append(':').Append(value);
    }

    private static void AppendPrefixEtagPart(StringBuilder builder, string? normalizedPrefix)
    {
        if (normalizedPrefix is null)
            return;

        AppendEtagPart(builder, "prefix-v2");
        AppendEtagPart(builder, normalizedPrefix);
    }

    internal static bool MatchesIfNoneMatch(string? headerValue, string etag)
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

    private static GetAllConfigValuesResult Failure(string error, string errorCode) =>
        new(false, null, error, ErrorCode: errorCode);
}
