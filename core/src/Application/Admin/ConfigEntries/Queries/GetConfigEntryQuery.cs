using Mediator;
using Nona.Application.Admin.ConfigEntries;
using Nona.Application.Admin.ConfigEntries.DTOs;
using Nona.Application.Admin.Projects;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Interfaces;

namespace Nona.Application.Admin.ConfigEntries.Queries;

public record GetConfigEntryQuery(string ProjectId, string EnvironmentName, string Key) : IRequest<GetConfigEntryResult>;

public record GetConfigEntryResult(bool Success, ConfigEntryDto? ConfigEntry, string? Error);

public class GetConfigEntryQueryHandler(
    IProjectRepository projectRepository,
    IEnvironmentRepository environmentRepository,
    IConfigEntryRepository configEntryRepository,
    IProjectAccessService projectAccessService)
    : IRequestHandler<GetConfigEntryQuery, GetConfigEntryResult>
{
    public async ValueTask<GetConfigEntryResult> Handle(GetConfigEntryQuery request, CancellationToken cancellationToken)
    {
        var project = await ProjectResolution.ResolveProjectAsync(projectRepository, request.ProjectId, cancellationToken);
        if (project is null)
            return new GetConfigEntryResult(false, null, "Project not found");

        var projectName = project.Name;
        if (!await projectAccessService.HasViewAccessAsync(projectName, cancellationToken))
            return new GetConfigEntryResult(false, null, "Access denied");

        if (!await environmentRepository.ExistsAsync(projectName, request.EnvironmentName, cancellationToken))
            return new GetConfigEntryResult(false, null, "Environment not found");

        var configEntry = await configEntryRepository.GetAsync(projectName, request.EnvironmentName, request.Key, cancellationToken);
        if (configEntry is null)
            return new GetConfigEntryResult(false, null, "Config entry not found");

        return new GetConfigEntryResult(true, ConfigEntryMapping.ToDto(configEntry), null);
    }
}
