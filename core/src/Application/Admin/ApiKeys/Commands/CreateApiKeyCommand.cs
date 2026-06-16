using MediatR;
using Nona.Application.Admin.ApiKeys.DTOs;
using Nona.Application.Admin.Projects;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Entities;
using Nona.Domain.Enums;
using Nona.Domain.Interfaces;
using System.Security.Cryptography;

namespace Nona.Application.Admin.ApiKeys.Commands;

public record CreateApiKeyRequest(string Name, string? Environment = null, string? Scope = null);

public record CreateApiKeyCommand(string ProjectId, string Name, string? Environment, string? Scope)
    : IRequest<CreateApiKeyResult>;

public record CreateApiKeyResult(bool Success, ApiKeyDto? ApiKey, string? Error);

public class CreateApiKeyCommandHandler(
    IProjectRepository projectRepository,
    IApiKeyRepository apiKeyRepository,
    IEnvironmentRepository environmentRepository,
    IProjectAccessService projectAccessService,
    IDateTime dateTime) : IRequestHandler<CreateApiKeyCommand, CreateApiKeyResult>
{
    public async Task<CreateApiKeyResult> Handle(CreateApiKeyCommand request, CancellationToken cancellationToken)
    {
        var project = await ProjectResolution.ResolveProjectAsync(projectRepository, request.ProjectId, cancellationToken);
        if (project is null)
            return new CreateApiKeyResult(false, null, "Project not found");

        if (!await projectAccessService.HasEditAccessAsync(project.Name, cancellationToken))
            return new CreateApiKeyResult(false, null, "Access denied");

        if (!TryParseScope(request.Scope, out var scope))
            return new CreateApiKeyResult(false, null, "Invalid scope. Must be 'client', 'server', or 'all'.");

        var environment = string.IsNullOrWhiteSpace(request.Environment)
            ? null
            : request.Environment.Trim();

        if (environment is not null &&
            !await environmentRepository.ExistsAsync(project.Name, environment, cancellationToken))
        {
            return new CreateApiKeyResult(false, null, "Environment not found");
        }

        var now = dateTime.NowUtc;
        var apiKey = new ApiKey
        {
            Name = request.Name.Trim(),
            Key = await GenerateUniqueApiKeyAsync(apiKeyRepository, cancellationToken),
            Project = project.Name,
            Environment = environment,
            Scope = scope,
            CreatedAt = now,
            UpdatedAt = now
        };

        await apiKeyRepository.AddAsync(apiKey, cancellationToken);

        return new CreateApiKeyResult(true, apiKey.ToDto(), null);
    }

    private static bool TryParseScope(string? value, out KeyScope scope)
    {
        scope = KeyScope.Frontend;
        if (string.IsNullOrWhiteSpace(value))
            return true;

        switch (value.Trim().ToLowerInvariant())
        {
            case "client":
                scope = KeyScope.Frontend;
                return true;
            case "server":
                scope = KeyScope.Backend;
                return true;
            case "all":
                scope = KeyScope.All;
                return true;
            default:
                return false;
        }
    }

    private static async Task<string> GenerateUniqueApiKeyAsync(
        IApiKeyRepository apiKeyRepository,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var apiKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            if (await apiKeyRepository.GetByKeyAsync(apiKey, cancellationToken) is null)
                return apiKey;
        }

        throw new InvalidOperationException("Unable to generate a unique API key.");
    }
}
