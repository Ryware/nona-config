using MediatR;
using Nona.Application.Admin.Environments.DTOs;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Interfaces;

namespace Nona.Application.Admin.Environments.Queries;

public record ListEnvironmentsQuery(string ProjectId) : IRequest<ListEnvironmentsResult>;

public record ListEnvironmentsResult(bool Success, IReadOnlyList<EnvironmentDto>? Environments, string? Error);

public class ListEnvironmentsQueryHandler(
    IProjectRepository projectRepository,
    IEnvironmentRepository environmentRepository,
    IProjectMemberRepository projectMemberRepository,
    ICurrentUserService currentUserService) : IRequestHandler<ListEnvironmentsQuery, ListEnvironmentsResult>
{
    public async Task<ListEnvironmentsResult> Handle(ListEnvironmentsQuery request, CancellationToken cancellationToken)
    {
        if (!await projectRepository.ExistsAsync(request.ProjectId, cancellationToken))
            return new ListEnvironmentsResult(false, null, "Project not found");

        // Check access for non-admin users
        if (!currentUserService.IsAdmin)
        {
            var username = currentUserService.Username;
            if (string.IsNullOrEmpty(username))
                return new ListEnvironmentsResult(false, null, "Access denied");

            var hasAccess = await projectMemberRepository.ExistsAsync(username, request.ProjectId, cancellationToken);
            if (!hasAccess)
                return new ListEnvironmentsResult(false, null, "Access denied");
        }

        var environments = await environmentRepository.ListByProjectAsync(request.ProjectId, cancellationToken);

        var dtos = environments.Select(e => new EnvironmentDto(
            e.Name,
            e.Project,
            e.CreatedAt,
            e.UpdatedAt)).ToList();

        return new ListEnvironmentsResult(true, dtos, null);
    }
}
