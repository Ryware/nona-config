using Mediator;
using Nona.Application.Admin.Environments.DTOs;
using Nona.Application.Admin.Projects;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Interfaces;

namespace Nona.Application.Admin.Environments.Queries;

public record ListEnvironmentsQuery(string ProjectId) : IRequest<ListEnvironmentsResult>;

public record ListEnvironmentsResult(bool Success, IReadOnlyList<EnvironmentDto>? Environments, string? Error);

public class ListEnvironmentsQueryHandler(
    IProjectRepository projectRepository,
    IEnvironmentRepository environmentRepository,
    IProjectAccessService projectAccessService) : IRequestHandler<ListEnvironmentsQuery, ListEnvironmentsResult>
{
    public async ValueTask<ListEnvironmentsResult> Handle(ListEnvironmentsQuery request, CancellationToken cancellationToken)
    {
        var project = await ProjectResolution.ResolveProjectAsync(projectRepository, request.ProjectId, cancellationToken);
        if (project is null)
            return new ListEnvironmentsResult(false, null, "Project not found");

        if (!await projectAccessService.HasViewAccessAsync(project.Name, cancellationToken))
            return new ListEnvironmentsResult(false, null, "Access denied");

        var environments = await environmentRepository.ListByProjectAsync(project.Name, cancellationToken);

        var dtos = environments.Select(e => new EnvironmentDto(
            e.Name,
            e.Project,
            e.ActiveReleaseVersion,
            e.CreatedAt,
            e.UpdatedAt)).ToList();

        return new ListEnvironmentsResult(true, dtos, null);
    }
}
