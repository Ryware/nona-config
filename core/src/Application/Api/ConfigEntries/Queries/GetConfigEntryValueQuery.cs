using Mediator;
using Nona.Application.Common;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Enums;
using Nona.Domain.Interfaces;

namespace Nona.Application.Api.ConfigEntries.Queries;

public record GetConfigEntryValueQuery(string EnvironmentId, string Key) : IRequest<GetConfigEntryValueResult>;

public record GetConfigEntryValueResult(
    bool Success,
    string? Value,
    string? LogicalContentType,
    string? Error,
    string? ErrorCode = null);

public class GetConfigEntryValueQueryHandler(
    IApiKeyRepository apiKeyRepository,
    IEnvironmentRepository environmentRepository,
    IConfigEntryRepository configEntryRepository,
    IApiKeyService apiKeyService)
    : IRequestHandler<GetConfigEntryValueQuery, GetConfigEntryValueResult>
{
    public async ValueTask<GetConfigEntryValueResult> Handle(GetConfigEntryValueQuery request, CancellationToken cancellationToken)
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

        var configEntry = await configEntryRepository.GetAsync(
            project.Name,
            request.EnvironmentId,
            request.Key,
            cancellationToken);
        if (configEntry is null || (configEntry.Scope & apiKeyScope) == 0)
            return Failure("Config entry not found", RuntimeConfigErrorCodes.ConfigEntryNotFound);

        if (!await RuntimeApiKeyValidation.IsCurrentAsync(apiKeyRepository, apiKeyHash, lookupResult, cancellationToken))
            return Failure("Invalid API key", RuntimeConfigErrorCodes.InvalidApiKey);

        return Success(configEntry.Value, configEntry.ContentType);
    }

    private static GetConfigEntryValueResult Success(string value, string contentType) =>
        new(
            true,
            value,
            ConfigEntryContentTypes.Normalize(contentType) ?? ConfigEntryContentTypes.Infer(value),
            null);

    private static GetConfigEntryValueResult Failure(string error, string errorCode) =>
        new(false, null, null, error, errorCode);
}
