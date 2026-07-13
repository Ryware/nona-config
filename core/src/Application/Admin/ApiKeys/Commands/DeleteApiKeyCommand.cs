using Mediator;
using Nona.Application.Admin.Projects;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Interfaces;

namespace Nona.Application.Admin.ApiKeys.Commands;

public record DeleteApiKeyCommand(string ProjectId, long ApiKeyId) : IRequest<DeleteApiKeyResult>;

public record DeleteApiKeyResult(bool Success, string? Error);

public class DeleteApiKeyCommandHandler(
    IProjectRepository projectRepository,
    IApiKeyRepository apiKeyRepository,
    IProjectAccessService projectAccessService) : IRequestHandler<DeleteApiKeyCommand, DeleteApiKeyResult>
{
    public async ValueTask<DeleteApiKeyResult> Handle(DeleteApiKeyCommand request, CancellationToken cancellationToken)
    {
        var project = await ProjectResolution.ResolveProjectAsync(projectRepository, request.ProjectId, cancellationToken);
        if (project is null)
            return new DeleteApiKeyResult(false, "Project not found");

        if (!await projectAccessService.HasEditAccessAsync(project.Name, cancellationToken))
            return new DeleteApiKeyResult(false, "Access denied");

        var apiKey = await apiKeyRepository.GetByIdAsync(request.ApiKeyId, cancellationToken);
        if (apiKey is null ||
            !string.Equals(apiKey.Project, project.Name, StringComparison.OrdinalIgnoreCase))
        {
            return new DeleteApiKeyResult(false, "API key not found");
        }

        await apiKeyRepository.DeleteAsync(request.ApiKeyId, cancellationToken);

        return new DeleteApiKeyResult(true, null);
    }
}
