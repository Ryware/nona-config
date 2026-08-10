using Mediator;
using Nona.Application.Admin.Projects.DTOs;
using Nona.Application.Common;
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

        if (currentUser?.Role == UserRole.Admin)
        {
            return await ToDtosAsync(projects, _ => "admin", cancellationToken);
        }

        var username = currentUser?.Email;
        if (string.IsNullOrWhiteSpace(username))
            return [];

        var userProjects = await projectMemberRepository.ListByUserAsync(username, cancellationToken);
        var accessByProject = userProjects.ToDictionary(
            membership => membership.ProjectId,
            membership => membership.Role.ToApiString(),
            StringComparer.OrdinalIgnoreCase);

        var accessibleProjects = projects
            .Where(project => accessByProject.ContainsKey(project.Name))
            .ToList();

        return await ToDtosAsync(
            accessibleProjects,
            project => accessByProject[project.Name],
            cancellationToken);
    }

    private async Task<IReadOnlyList<ProjectDto>> ToDtosAsync(
        IEnumerable<Project> projects,
        Func<Project, string> accessLevel,
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
                accessLevel(project),
                environmentNames,
                project.CreatedAt,
                project.UpdatedAt));
        }

        return dtos;
    }
}
