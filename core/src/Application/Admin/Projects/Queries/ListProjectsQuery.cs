using MediatR;
using Nona.Application.Admin.Projects.DTOs;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Interfaces;

namespace Nona.Application.Admin.Projects.Queries;

public record ListProjectsQuery : IRequest<IReadOnlyList<ProjectDto>>;

public class ListProjectsQueryHandler(
    IProjectRepository projectRepository,
    IProjectMemberRepository projectMemberRepository,
    ICurrentUserService currentUserService) : IRequestHandler<ListProjectsQuery, IReadOnlyList<ProjectDto>>
{
    public async Task<IReadOnlyList<ProjectDto>> Handle(ListProjectsQuery request, CancellationToken cancellationToken)
    {
        var projects = await projectRepository.ListAsync(cancellationToken);

        // Admin users see all projects
        if (currentUserService.IsAdmin)
        {
            return projects.Select(p => new ProjectDto(
                p.Id,
                p.Name,
                p.UrlSlug,
                p.Environments,
                p.CreatedAt,
                p.UpdatedAt)).ToList();
        }

        // Non-admin users only see projects they have access to
        var username = currentUserService.Username;
        if (string.IsNullOrEmpty(username))
            return Array.Empty<ProjectDto>();

        var userProjects = await projectMemberRepository.ListByUserAsync(username, cancellationToken);
        var accessibleProjectNames = userProjects.Select(m => m.ProjectId).ToHashSet(StringComparer.OrdinalIgnoreCase);

        return projects
            .Where(p => accessibleProjectNames.Contains(p.Name))
            .Select(p => new ProjectDto(
                p.Id,
                p.Name,
                p.UrlSlug,
                p.Environments,
                p.CreatedAt,
                p.UpdatedAt)).ToList();
    }
}
