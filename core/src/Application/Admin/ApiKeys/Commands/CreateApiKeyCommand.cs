using Mediator;
using Nona.Application.Admin.ApiKeys.DTOs;
using Nona.Application.Admin.Projects;
using Nona.Application.Common;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Entities;
using Nona.Domain.Enums;
using Nona.Domain.Interfaces;

namespace Nona.Application.Admin.ApiKeys.Commands;

public record CreateApiKeyRequest(string Name, string? Environment = null, string? Scope = null);

public record CreateApiKeyCommand(string ProjectId, string Name, string? Environment, string? Scope)
    : IRequest<CreateApiKeyResult>;

public record CreateApiKeyResult(bool Success, CreatedApiKeyDto? ApiKey, string? Error);

public class CreateApiKeyCommandHandler(
    IProjectRepository projectRepository,
    IApiKeyRepository apiKeyRepository,
    IEnvironmentRepository environmentRepository,
    IProjectAccessService projectAccessService,
    IDateTime dateTime) : IRequestHandler<CreateApiKeyCommand, CreateApiKeyResult>
{
    public async ValueTask<CreateApiKeyResult> Handle(CreateApiKeyCommand request, CancellationToken cancellationToken)
    {
        var project = await ProjectResolution.ResolveProjectAsync(projectRepository, request.ProjectId, cancellationToken);
        if (project is null)
            return new CreateApiKeyResult(false, null, "Project not found");

        if (!await projectAccessService.HasEditAccessAsync(project.Name, cancellationToken))
            return new CreateApiKeyResult(false, null, "Access denied");

        if (!TryParseScope(request.Scope, out var scope))
            return new CreateApiKeyResult(false, null, "Invalid scope. Must be 'client', 'server', or 'all'.");

        string? environment = null;
        if (!string.IsNullOrWhiteSpace(request.Environment))
        {
            var requestedEnvironment = request.Environment.Trim();
            var resolvedEnvironment = await environmentRepository.GetAsync(
                project.Name,
                requestedEnvironment,
                cancellationToken);
            if (resolvedEnvironment is null)
                return new CreateApiKeyResult(false, null, "Environment not found");

            environment = resolvedEnvironment.Name;
        }

        var secret = await GenerateUniqueApiKeyAsync(apiKeyRepository, cancellationToken);
        var now = dateTime.NowUtc;
        var apiKey = new ApiKey
        {
            Name = request.Name.Trim(),
            KeyHash = ApiKeySecret.Hash(secret),
            Fingerprint = ApiKeySecret.Fingerprint(secret),
            HashVersion = ApiKeySecret.CurrentHashVersion,
            Project = project.Name,
            Environment = environment,
            Scope = scope,
            CreatedAt = now,
            UpdatedAt = now
        };

        await apiKeyRepository.AddAsync(apiKey, cancellationToken);

        return new CreateApiKeyResult(true, apiKey.ToCreatedDto(secret), null);
    }

    private static bool TryParseScope(string? value, out KeyScope scope)
    {
        // An unspecified scope defaults to client (frontend) for API keys.
        if (string.IsNullOrWhiteSpace(value))
        {
            scope = KeyScope.Frontend;
            return true;
        }

        var parsed = EnumExtensions.ParseKeyScope(value);
        scope = parsed ?? KeyScope.Frontend;
        return parsed is not null;
    }

    private static async Task<string> GenerateUniqueApiKeyAsync(
        IApiKeyRepository apiKeyRepository,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var secret = ApiKeySecret.Generate();
            var keyHash = ApiKeySecret.Hash(secret);
            if (await apiKeyRepository.GetByKeyHashAsync(keyHash, cancellationToken) is null)
                return secret;
        }

        throw new InvalidOperationException("Unable to generate a unique API key.");
    }
}
