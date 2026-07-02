using Mediator;
using Nona.Application.Admin.ParameterShareLinks.DTOs;
using Nona.Application.Admin.Projects;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Interfaces;

namespace Nona.Application.Admin.ParameterShareLinks.Queries;

public record ListParameterShareLinksQuery(
    string ProjectId,
    string EnvironmentName,
    string Key) : IRequest<ListParameterShareLinksResult>;

public record ListParameterShareLinksResult(
    bool Success,
    IReadOnlyList<ParameterShareLinkDto>? ShareLinks,
    string? Error);

public class ListParameterShareLinksQueryHandler(
    IProjectRepository projectRepository,
    IConfigEntryRepository configEntryRepository,
    IParameterShareLinkRepository shareLinkRepository,
    IProjectAccessService projectAccessService)
    : IRequestHandler<ListParameterShareLinksQuery, ListParameterShareLinksResult>
{
    public async ValueTask<ListParameterShareLinksResult> Handle(
        ListParameterShareLinksQuery request,
        CancellationToken cancellationToken)
    {
        var project = await ProjectResolution.ResolveProjectAsync(projectRepository, request.ProjectId, cancellationToken);
        if (project is null)
            return new ListParameterShareLinksResult(false, null, "Project not found");

        var projectName = project.Name;
        if (!await projectAccessService.HasEditAccessAsync(projectName, cancellationToken))
            return new ListParameterShareLinksResult(false, null, "Access denied");

        if (!await configEntryRepository.ExistsAsync(projectName, request.EnvironmentName, request.Key, cancellationToken))
            return new ListParameterShareLinksResult(false, null, "Config entry not found");

        var links = await shareLinkRepository.ListByConfigEntryAsync(
            projectName,
            request.EnvironmentName,
            request.Key,
            cancellationToken);

        return new ListParameterShareLinksResult(
            true,
            links.Select(ParameterShareLinkMapping.ToDto).ToList(),
            null);
    }
}
