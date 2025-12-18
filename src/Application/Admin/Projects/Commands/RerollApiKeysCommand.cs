using MediatR;
using Nona.Application.Admin.Projects.DTOs;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Interfaces;
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
        var project = await projectRepository.GetByNameAsync(request.ProjectId, cancellationToken);
        if (project is null)
            return new RerollApiKeysResult(false, null, "Project not found");

        if (!await projectAccessService.HasAdminAccessAsync(request.ProjectId, cancellationToken))
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
            project.Name,
            project.ServerApiKey,
            project.ClientApiKey,
            project.Environments,
            project.CreatedAt,
            project.UpdatedAt);

        return new RerollApiKeysResult(true, dto, null);
    }

    private static string GenerateApiKey()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    }
}
