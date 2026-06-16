using MediatR;
using Nona.Application.Admin.Projects;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Interfaces;

namespace Nona.Application.Admin.ConfigEntries.Commands;

public record DeleteConfigEntryCommand(string ProjectId, string EnvironmentId, string Key) : IRequest<DeleteConfigEntryResult>;

public record DeleteConfigEntryResult(bool Success, string? Error);

public class DeleteConfigEntryCommandHandler(
    IProjectRepository projectRepository,
    IEnvironmentRepository environmentRepository,
    IConfigEntryRepository configEntryRepository,
    IProjectAccessService projectAccessService,
    IAuditLogService? auditLogService = null)
    : IRequestHandler<DeleteConfigEntryCommand, DeleteConfigEntryResult>
{
    public async Task<DeleteConfigEntryResult> Handle(DeleteConfigEntryCommand request, CancellationToken cancellationToken)
    {
        var project = await ProjectResolution.ResolveProjectAsync(projectRepository, request.ProjectId, cancellationToken);
        if (project is null)
            return new DeleteConfigEntryResult(false, "Project not found");

        var projectName = project.Name;
        if (!await projectAccessService.HasEditAccessAsync(projectName, cancellationToken))
            return new DeleteConfigEntryResult(false, "Access denied");

        if (!await environmentRepository.ExistsAsync(projectName, request.EnvironmentId, cancellationToken))
            return new DeleteConfigEntryResult(false, "Environment not found");


        if (!await configEntryRepository.ExistsAsync(projectName, request.EnvironmentId, request.Key, cancellationToken))
            return new DeleteConfigEntryResult(false, "Config entry not found");


        await configEntryRepository.DeleteAsync(projectName, request.EnvironmentId, request.Key, cancellationToken);

        if (auditLogService is not null)
        {
            await auditLogService.WriteAsync(
                "Deleted Key",
                request.Key,
                project: projectName,
                environment: request.EnvironmentId,
                cancellationToken: cancellationToken);
        }

        return new DeleteConfigEntryResult(true, null);
    }
}
