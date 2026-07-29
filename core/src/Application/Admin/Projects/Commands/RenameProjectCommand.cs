using Mediator;
using Nona.Application.Admin.Projects.DTOs;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Enums;
using Nona.Domain.Interfaces;

namespace Nona.Application.Admin.Projects.Commands;

public record RenameProjectRequest(string Name);

public record RenameProjectCommand(string ProjectId, string NewName) : IRequest<RenameProjectResult>;

public record RenameProjectResult(bool Success, ProjectDto? Project, string? Error);

public class RenameProjectCommandHandler(
    IProjectRepository projectRepository,
    IEnvironmentRepository environmentRepository,
    IUserAuthorizationService userAuthorizationService,
    IDateTime dateTime,
    IAuditLogService? auditLogService = null)
    : IRequestHandler<RenameProjectCommand, RenameProjectResult>
{
    public async ValueTask<RenameProjectResult> Handle(
        RenameProjectCommand request,
        CancellationToken cancellationToken)
    {
        var project = await ProjectResolution.ResolveProjectAsync(
            projectRepository,
            request.ProjectId,
            cancellationToken);
        if (project is null)
        {
            return new RenameProjectResult(false, null, "Project not found");
        }

        var currentUser = await userAuthorizationService.GetCurrentUserAsync(cancellationToken);
        if (currentUser?.IsAdmin != true)
        {
            return new RenameProjectResult(
                false,
                null,
                "Access denied. Only admin users can rename projects.");
        }

        if (!project.Name.Equals(request.NewName, StringComparison.OrdinalIgnoreCase)
            && await projectRepository.ExistsAsync(request.NewName, cancellationToken))
        {
            return new RenameProjectResult(false, null, "Project already exists");
        }

        var environments = await environmentRepository.ListByProjectAsync(project.Name, cancellationToken);
        var updatedAt = dateTime.NowUtc;
        await projectRepository.RenameAsync(
            project.Name,
            request.NewName,
            updatedAt,
            cancellationToken);

        if (auditLogService is not null)
        {
            await auditLogService.WriteAsync(
                AuditActionKind.Update,
                "Renamed Project",
                $"{project.Name} to {request.NewName}",
                project: request.NewName,
                cancellationToken: cancellationToken);
        }

        return new RenameProjectResult(
            true,
            new ProjectDto(
                project.Id,
                request.NewName,
                project.UrlSlug,
                environments.Select(environment => environment.Name).ToList(),
                project.CreatedAt,
                updatedAt),
            null);
    }
}
