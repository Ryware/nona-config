using Mediator;
using Nona.Application.Admin.Projects;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Interfaces;

namespace Nona.Application.Admin.Environments.Commands;

public record DeleteEnvironmentCommand(string ProjectId, string EnvironmentId) : IRequest<DeleteEnvironmentResult>;

public record DeleteEnvironmentResult(bool Success, string? Error);

public class DeleteEnvironmentCommandHandler(
    IProjectRepository projectRepository,
    IEnvironmentRepository environmentRepository,
    IConfigEntryRepository configEntryRepository,
    IProjectAccessService projectAccessService,
    IAuditLogService? auditLogService = null) : IRequestHandler<DeleteEnvironmentCommand, DeleteEnvironmentResult>
{
    public async ValueTask<DeleteEnvironmentResult> Handle(DeleteEnvironmentCommand request, CancellationToken cancellationToken)
    {
        var project = await ProjectResolution.ResolveProjectAsync(projectRepository, request.ProjectId, cancellationToken);
        if (project is null)
            return new DeleteEnvironmentResult(false, "Project not found");

        var projectName = project.Name;
        if (!await projectAccessService.HasEditAccessAsync(projectName, cancellationToken))
            return new DeleteEnvironmentResult(false, "Access denied");

        if (!await environmentRepository.ExistsAsync(projectName, request.EnvironmentId, cancellationToken))
            return new DeleteEnvironmentResult(false, "Environment not found");

        await DeleteEnvironmentConfigEntries(projectName, request.EnvironmentId, cancellationToken);

        await environmentRepository.DeleteAsync(projectName, request.EnvironmentId, cancellationToken);

        if (auditLogService is not null)
        {
            await auditLogService.WriteAsync(
                "Deleted Environment",
                request.EnvironmentId,
                project: projectName,
                environment: request.EnvironmentId,
                cancellationToken: cancellationToken);
        }

        return new DeleteEnvironmentResult(true, null);
    }

    private async Task DeleteEnvironmentConfigEntries(string projectName, string environmentId, CancellationToken cancellationToken)
    {
        var configEntries = await configEntryRepository.ListAsync(projectName, environmentId, cancellationToken);
        var keys = configEntries.Select(e => e.Key).ToList();
        if (keys.Count > 0)
        {
            await configEntryRepository.DeleteManyAsync(projectName, environmentId, keys, cancellationToken);
        }
    }
}
