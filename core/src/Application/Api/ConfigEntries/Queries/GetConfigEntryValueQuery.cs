using MediatR;
using Nona.Application.Common;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Interfaces;

namespace Nona.Application.Api.ConfigEntries.Queries;

public record GetConfigEntryValueQuery(string EnvironmentId, string Key) : IRequest<GetConfigEntryValueResult>;

public record GetConfigEntryValueResult(bool Success, string? Value, string? LogicalContentType, string? Error);

public class GetConfigEntryValueQueryHandler(
    IApiKeyRepository apiKeyRepository,
    IEnvironmentRepository environmentRepository,
    IConfigEntryRepository configEntryRepository,
    IApiKeyService apiKeyService)
    : IRequestHandler<GetConfigEntryValueQuery, GetConfigEntryValueResult>
{
    public async Task<GetConfigEntryValueResult> Handle(GetConfigEntryValueQuery request, CancellationToken cancellationToken)
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

        if (!await environmentRepository.ExistsAsync(project.Name, request.EnvironmentId, cancellationToken))
            return new GetConfigEntryValueResult(false, null, null, "Environment not found");

        var configEntry = await configEntryRepository.GetAsync(project.Name, request.EnvironmentId, request.Key, cancellationToken);
        if (configEntry is null)
            return new GetConfigEntryValueResult(false, null, null, "Config entry not found");

        if ((apiKeyScope & configEntry.Scope) == 0)
            return new GetConfigEntryValueResult(false, null, null, "Config entry not found");

        var contentType = ConfigEntryContentTypes.Normalize(configEntry.ContentType)
            ?? ConfigEntryContentTypes.Infer(configEntry.Value);

        return new GetConfigEntryValueResult(true, configEntry.Value, contentType, null);
    }
}
