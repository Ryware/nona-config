using Mediator;
using Nona.Application.Admin.ConfigReleases.DTOs;
using Nona.Application.Admin.Projects;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Interfaces;

namespace Nona.Application.Admin.ConfigReleases.Queries;

public record ListConfigReleasesQuery(string ProjectId, string EnvironmentName) : IRequest<ListConfigReleasesResult>;

public record ListConfigReleasesResult(bool Success, IReadOnlyList<ConfigReleaseDto>? Releases, string? Error);

public class ListConfigReleasesQueryHandler(
    IProjectRepository projectRepository,
    IEnvironmentRepository environmentRepository,
    IConfigReleaseRepository configReleaseRepository,
    IProjectAccessService projectAccessService)
    : IRequestHandler<ListConfigReleasesQuery, ListConfigReleasesResult>
{
    public async ValueTask<ListConfigReleasesResult> Handle(ListConfigReleasesQuery request, CancellationToken cancellationToken)
    {
        var project = await ProjectResolution.ResolveProjectAsync(projectRepository, request.ProjectId, cancellationToken);
        if (project is null)
            return new ListConfigReleasesResult(false, null, "Project not found");

        var projectName = project.Name;
        if (!await projectAccessService.HasViewAccessAsync(projectName, cancellationToken))
            return new ListConfigReleasesResult(false, null, "Access denied");

        var environment = await environmentRepository.GetAsync(projectName, request.EnvironmentName, cancellationToken);
        if (environment is null)
            return new ListConfigReleasesResult(false, null, "Environment not found");

        var releases = await configReleaseRepository.ListAsync(projectName, request.EnvironmentName, cancellationToken);
        return new ListConfigReleasesResult(
            true,
            releases.Select(release => ConfigReleaseMapping.ToDto(release, environment.ActiveReleaseVersion)).ToList(),
            null);
    }
}
