using MediatR;
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
        if (!await projectRepository.ExistsAsync(request.ProjectId, cancellationToken))
            return new DeleteConfigEntryResult(false, "Project not found");

        if (!await projectAccessService.HasAdminAccessAsync(request.ProjectId, cancellationToken))
            return new DeleteConfigEntryResult(false, "Access denied");

        if (!await environmentRepository.ExistsAsync(request.ProjectId, request.EnvironmentId, cancellationToken))
            return new DeleteConfigEntryResult(false, "Environment not found");


        if (!await configEntryRepository.ExistsAsync(request.ProjectId, request.EnvironmentId, request.Key, cancellationToken))
            return new DeleteConfigEntryResult(false, "Config entry not found");


        await configEntryRepository.DeleteAsync(request.ProjectId, request.EnvironmentId, request.Key, cancellationToken);

        if (auditLogService is not null)
        {
            await auditLogService.WriteAsync(
                "Deleted Key",
                request.Key,
                project: request.ProjectId,
                environment: request.EnvironmentId,
                cancellationToken: cancellationToken);
        }

        return new DeleteConfigEntryResult(true, null);
    }
}
