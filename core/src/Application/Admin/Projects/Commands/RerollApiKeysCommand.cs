using MediatR;
using Nona.Application.Admin.Projects.DTOs;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Interfaces;
using Nona.Domain.Entities;
using System.Linq;
using System.Security.Cryptography;

namespace Nona.Application.Admin.Projects.Commands;

public enum ApiKeyType
{
    Server,
    Client,
    Both
}

public record RerollApiKeysRequest(string KeyType);
public record RerollApiKeysCommand(string ProjectId, ApiKeyType KeyType) : IRequest<RerollApiKeysResult>;

public record RerollApiKeysResult(bool Success, ProjectDto? Project, string? Error);

public class RerollApiKeysCommandHandler(
    IProjectRepository projectRepository,
    IProjectAccessService projectAccessService,
    IDateTime dateTime) : IRequestHandler<RerollApiKeysCommand, RerollApiKeysResult>
{
    public async Task<RerollApiKeysResult> Handle(RerollApiKeysCommand request, CancellationToken cancellationToken)
    {
        // Resolve project by name/id/slug
        var project = await ResolveProjectAsync(request.ProjectId, cancellationToken);
        if (project is null)
            return new RerollApiKeysResult(false, null, "Project not found");

        if (!await projectAccessService.HasAdminAccessAsync(project.Name, cancellationToken))
            return new RerollApiKeysResult(false, null, "Access denied");

        switch (request.KeyType)
        {
            case ApiKeyType.Server:
                project.ServerApiKey = GenerateApiKey();
                break;
            case ApiKeyType.Client:
                project.ClientApiKey = GenerateApiKey();
                break;
            case ApiKeyType.Both:
                project.ServerApiKey = GenerateApiKey();
                project.ClientApiKey = GenerateApiKey();
                break;
        }
        project.UpdatedAt = dateTime.NowUtc;
        await projectRepository.UpdateAsync(project, cancellationToken);

        var dto = new ProjectDto(
            project.Id,
            project.Name,
            project.UrlSlug,
            project.ServerApiKey,
            project.ClientApiKey,
            project.Environments,
            project.CreatedAt,
            project.UpdatedAt);

        return new RerollApiKeysResult(true, dto, null);
    }

    private async Task<Project?> ResolveProjectAsync(string idOrNameOrSlug, CancellationToken cancellationToken)
    {
        var project = await projectRepository.GetByNameAsync(idOrNameOrSlug, cancellationToken);
        if (project != null)
            return project;

        var projects = await projectRepository.ListAsync(cancellationToken);

        if (long.TryParse(idOrNameOrSlug, out var numericId))
        {
            var byId = projects.FirstOrDefault(p => p.Id == numericId);
            if (byId != null)
                return byId;
        }

        var bySlug = projects.FirstOrDefault(p => !string.IsNullOrEmpty(p.UrlSlug) && string.Equals(p.UrlSlug, idOrNameOrSlug, StringComparison.OrdinalIgnoreCase));
        return bySlug;
    }

    private static string GenerateApiKey()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    }
}
