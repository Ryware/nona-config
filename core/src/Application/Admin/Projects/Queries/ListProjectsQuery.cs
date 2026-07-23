using Mediator;
using Nona.Application.Admin.Projects.DTOs;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Entities;
using Nona.Domain.Interfaces;

namespace Nona.Application.Admin.Projects.Queries;

public record ListProjectsQuery : IRequest<IReadOnlyList<ProjectDto>>;

public class ListProjectsQueryHandler(
    IProjectRepository projectRepository,
    IEnvironmentRepository environmentRepository,
    IProjectMemberRepository projectMemberRepository,
    IUserAuthorizationService userAuthorizationService) : IRequestHandler<ListProjectsQuery, IReadOnlyList<ProjectDto>>
{
    public async ValueTask<IReadOnlyList<ProjectDto>> Handle(ListProjectsQuery request, CancellationToken cancellationToken)
    {
        var projects = await projectRepository.ListAsync(cancellationToken);

        var currentUser = await userAuthorizationService.GetCurrentUserAsync(cancellationToken);

        if (currentUser?.IsAdmin == true || currentUser?.Role == Nona.Domain.Entities.UserRole.Editor)
        {
            return await ToDtosAsync(projects, cancellationToken);
        }

        // Non-admin users only see projects they have access to
        var username = currentUser?.Email;
        if (string.IsNullOrWhiteSpace(username))
            return Array.Empty<ProjectDto>();

        var userProjects = await projectMemberRepository.ListByUserAsync(username, cancellationToken);
        var accessibleProjectNames = userProjects.Select(m => m.ProjectId).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var accessibleProjects = projects
            .Where(p => accessibleProjectNames.Contains(p.Name))
            .ToList();

        return await ToDtosAsync(accessibleProjects, cancellationToken);
    }

    private async Task<IReadOnlyList<ProjectDto>> ToDtosAsync(
        IEnumerable<Project> projects,
        CancellationToken cancellationToken)
    {
        var dtos = new List<ProjectDto>();

        foreach (var project in projects)
        {
            var environments = await environmentRepository.ListByProjectAsync(project.Name, cancellationToken);
            var environmentNames = environments
                .Select(environment => environment.Name)
                .ToList();

            dtos.Add(new ProjectDto(
                project.Id,
                project.Name,
                project.UrlSlug,
                environmentNames,
                project.CreatedAt,
                project.UpdatedAt));
        }

        return dtos;
    }
}
