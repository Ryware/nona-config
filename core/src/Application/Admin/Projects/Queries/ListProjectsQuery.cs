using MediatR;
using Nona.Application.Admin.Projects.DTOs;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Interfaces;

namespace Nona.Application.Admin.Projects.Queries;

public record ListProjectsQuery : IRequest<IReadOnlyList<ProjectDto>>;

public class ListProjectsQueryHandler(
    IProjectRepository projectRepository,
    IProjectMemberRepository projectMemberRepository,
    IUserAuthorizationService userAuthorizationService) : IRequestHandler<ListProjectsQuery, IReadOnlyList<ProjectDto>>
{
    public async Task<IReadOnlyList<ProjectDto>> Handle(ListProjectsQuery request, CancellationToken cancellationToken)
    {
        var projects = await projectRepository.ListAsync(cancellationToken);

        var currentUser = await userAuthorizationService.GetCurrentUserAsync(cancellationToken);

        if (currentUser?.IsAdmin == true || currentUser?.Role == Nona.Domain.Entities.UserRole.Editor)
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
        var username = currentUser?.Email;
        if (string.IsNullOrWhiteSpace(username))
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
