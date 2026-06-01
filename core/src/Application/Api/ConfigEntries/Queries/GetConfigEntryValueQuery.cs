using MediatR;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Entities;
using Nona.Domain.Enums;
using Nona.Domain.Interfaces;

namespace Nona.Application.Api.ConfigEntries.Queries;

public record GetConfigEntryValueQuery(string EnvironmentId, string Key) : IRequest<GetConfigEntryValueResult>;

public record GetConfigEntryValueResult(bool Success, string? Value, string? ContentType, string? Error);

public class GetConfigEntryValueQueryHandler(
    IProjectRepository projectRepository,
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
        Project project;
        KeyScope apiKeyScope;
        string? apiKeyEnvironment;

        if (lookupResult is not null)
        {
            (project, apiKeyScope, apiKeyEnvironment) = lookupResult;
        }
        else
        {
            var legacyLookupResult = await projectRepository.GetByApiKeyAsync(apiKey, cancellationToken);
            if (legacyLookupResult is null)
                return new GetConfigEntryValueResult(false, null, null, "Invalid API key");

            (project, apiKeyScope, apiKeyEnvironment) = legacyLookupResult;
        }

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

        // Check if the API key scope has access to this config entry
        if (!configEntry.Scope.HasFlag(apiKeyScope))
            return new GetConfigEntryValueResult(false, null, null, "Config entry not found");

        return new GetConfigEntryValueResult(true, configEntry.Value, configEntry.ContentType, null);
    }
}
