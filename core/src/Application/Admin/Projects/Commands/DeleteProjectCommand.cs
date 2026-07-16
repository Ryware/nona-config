using Mediator;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Entities;
using Nona.Domain.Interfaces;

namespace Nona.Application.Admin.Projects.Commands;

public record DeleteProjectCommand(string ProjectId) : IRequest<DeleteProjectResult>;

public record DeleteProjectResult(bool Success, string? Error);

public class DeleteProjectCommandHandler(
    IProjectRepository projectRepository,
    IEnvironmentRepository environmentRepository,
    IConfigEntryRepository configEntryRepository,
    IProjectMemberRepository projectMemberRepository,
    IUserAuthorizationService userAuthorizationService,
    IConfigReleaseRepository? configReleaseRepository = null,
    IAuditLogService? auditLogService = null)
    : IRequestHandler<DeleteProjectCommand, DeleteProjectResult>
{
    public async ValueTask<DeleteProjectResult> Handle(DeleteProjectCommand request, CancellationToken cancellationToken)
    {
        // Resolve the project by name, id, or slug
        var project = await ResolveProjectAsync(request.ProjectId, cancellationToken);
        if (project is null)
            return new DeleteProjectResult(false, "Project not found");

        // Only admin users can delete projects
        var currentUser = await userAuthorizationService.GetCurrentUserAsync(cancellationToken);
        if (currentUser?.IsAdmin != true)
            return new DeleteProjectResult(false, "Access denied. Only admin users can delete projects.");

        var projectName = project.Name;

        await DeleteConfigEntriesAsync(projectName, cancellationToken);
        if (configReleaseRepository is not null)
        {
            await configReleaseRepository.DeleteByProjectAsync(projectName, cancellationToken);
        }

        await DeleteEnvironmentsAsync(projectName, cancellationToken);

        await projectMemberRepository.DeleteByProjectAsync(projectName, cancellationToken);

        await projectRepository.DeleteAsync(projectName, cancellationToken);

        if (auditLogService is not null)
        {
            await auditLogService.WriteAsync(
                "Deleted Project",
                projectName,
                project: projectName,
                cancellationToken: cancellationToken);
        }

        return new DeleteProjectResult(true, null);
    }
    private async Task DeleteEnvironmentsAsync(string projectName, CancellationToken cancellationToken)
    {
        var environments = await environmentRepository.ListByProjectAsync(projectName, cancellationToken);
        foreach (var env in environments)
        {
            await environmentRepository.DeleteAsync(projectName, env.Name, cancellationToken);
        }
    }

    private async Task DeleteConfigEntriesAsync(string projectName, CancellationToken cancellationToken)
    {
        var configEntries = await configEntryRepository.ListByProjectAsync(projectName, cancellationToken);
        foreach (var entry in configEntries)
        {
            await configEntryRepository.DeleteAsync(entry.Project, entry.Environment, entry.Key, cancellationToken);
        }
    }

    private async Task<Project?> ResolveProjectAsync(string idOrNameOrSlug, CancellationToken cancellationToken)
    {
        // Try direct name lookup
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
}
