using Mediator;
using Nona.Application.Admin.Environments.DTOs;
using Nona.Application.Admin.Projects;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Interfaces;

namespace Nona.Application.Admin.Environments.Commands;

public record RenameEnvironmentRequest(string Name);

public record RenameEnvironmentCommand(string ProjectId, string CurrentName, string NewName)
    : IRequest<RenameEnvironmentResult>;

public record RenameEnvironmentResult(bool Success, EnvironmentDto? Environment, string? Error);

public class RenameEnvironmentCommandHandler(
    IProjectRepository projectRepository,
    IEnvironmentRepository environmentRepository,
    IProjectAccessService projectAccessService,
    IDateTime dateTime,
    IAuditLogService? auditLogService = null)
    : IRequestHandler<RenameEnvironmentCommand, RenameEnvironmentResult>
{
    public async ValueTask<RenameEnvironmentResult> Handle(
        RenameEnvironmentCommand request,
        CancellationToken cancellationToken)
    {
        var project = await ProjectResolution.ResolveProjectAsync(
            projectRepository,
            request.ProjectId,
            cancellationToken);
        if (project is null)
        {
            return new RenameEnvironmentResult(false, null, "Project not found");
        }

        if (!await projectAccessService.HasEditAccessAsync(project.Name, cancellationToken))
        {
            return new RenameEnvironmentResult(false, null, "Access denied");
        }

        var environment = await environmentRepository.GetAsync(
            project.Name,
            request.CurrentName,
            cancellationToken);
        if (environment is null)
        {
            return new RenameEnvironmentResult(false, null, "Environment not found");
        }

        if (!request.CurrentName.Equals(request.NewName, StringComparison.OrdinalIgnoreCase)
            && await environmentRepository.ExistsAsync(project.Name, request.NewName, cancellationToken))
        {
            return new RenameEnvironmentResult(false, null, "Environment already exists");
        }

        var updatedAt = dateTime.NowUtc;
        await environmentRepository.RenameAsync(
            project.Name,
            environment.Name,
            request.NewName,
            updatedAt,
            cancellationToken);

        if (auditLogService is not null)
        {
            await auditLogService.WriteAsync(
                "Renamed Environment",
                $"{environment.Name} to {request.NewName}",
                project: project.Name,
                environment: request.NewName,
                cancellationToken: cancellationToken);
        }

        return new RenameEnvironmentResult(
            true,
            new EnvironmentDto(
                request.NewName,
                project.Name,
                environment.ActiveReleaseVersion,
                environment.CreatedAt,
                updatedAt),
            null);
    }
}
