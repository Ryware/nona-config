using Mediator;
using Nona.Application.Admin.ConfigReleases.DTOs;
using Nona.Application.Admin.Projects;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Interfaces;

namespace Nona.Application.Admin.ConfigReleases.Queries;

public record GetConfigReleaseQuery(string ProjectId, string EnvironmentName, string Version) : IRequest<GetConfigReleaseResult>;

public record GetConfigReleaseResult(bool Success, ConfigReleaseDetailsDto? Release, string? Error);

public class GetConfigReleaseQueryHandler(
    IProjectRepository projectRepository,
    IEnvironmentRepository environmentRepository,
    IConfigReleaseRepository configReleaseRepository,
    IProjectAccessService projectAccessService)
    : IRequestHandler<GetConfigReleaseQuery, GetConfigReleaseResult>
{
    public async ValueTask<GetConfigReleaseResult> Handle(GetConfigReleaseQuery request, CancellationToken cancellationToken)
    {
        var project = await ProjectResolution.ResolveProjectAsync(projectRepository, request.ProjectId, cancellationToken);
        if (project is null)
            return new GetConfigReleaseResult(false, null, "Project not found");

        var projectName = project.Name;
        if (!await projectAccessService.HasViewAccessAsync(projectName, cancellationToken))
            return new GetConfigReleaseResult(false, null, "Access denied");

        var environment = await environmentRepository.GetAsync(projectName, request.EnvironmentName, cancellationToken);
        if (environment is null)
            return new GetConfigReleaseResult(false, null, "Environment not found");

        if (!ConfigReleaseVersions.TryParseExact(request.Version, out var version))
            return new GetConfigReleaseResult(false, null, "Version must use major.minor.patch format.");

        var release = await configReleaseRepository.GetAsync(projectName, request.EnvironmentName, version.Normalized, cancellationToken);
        if (release is null)
            return new GetConfigReleaseResult(false, null, "Release not found");

        return new GetConfigReleaseResult(
            true,
            ConfigReleaseMapping.ToDetailsDto(release, environment.ActiveReleaseVersion),
            null);
    }
}
