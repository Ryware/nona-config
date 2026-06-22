using MediatR;
using Nona.Application.Admin.ConfigEntries.DTOs;
using Nona.Application.Admin.Projects;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Interfaces;

namespace Nona.Application.Admin.ConfigEntries.Queries;

public record ListConfigEntryVersionsQuery(string ProjectId, string EnvironmentName, string Key) : IRequest<ListConfigEntryVersionsResult>;

public record ListConfigEntryVersionsResult(bool Success, IReadOnlyList<ConfigEntryVersionDto>? Versions, string? Error);

public class ListConfigEntryVersionsQueryHandler(
    IProjectRepository projectRepository,
    IEnvironmentRepository environmentRepository,
    IConfigEntryRepository configEntryRepository,
    IProjectAccessService projectAccessService)
    : IRequestHandler<ListConfigEntryVersionsQuery, ListConfigEntryVersionsResult>
{
    public async Task<ListConfigEntryVersionsResult> Handle(ListConfigEntryVersionsQuery request, CancellationToken cancellationToken)
    {
        var project = await ProjectResolution.ResolveProjectAsync(projectRepository, request.ProjectId, cancellationToken);
        if (project is null)
            return new ListConfigEntryVersionsResult(false, null, "Project not found");

        var projectName = project.Name;
        if (!await projectAccessService.HasViewAccessAsync(projectName, cancellationToken))
            return new ListConfigEntryVersionsResult(false, null, "Access denied");

        if (!await environmentRepository.ExistsAsync(projectName, request.EnvironmentName, cancellationToken))
            return new ListConfigEntryVersionsResult(false, null, "Environment not found");

        if (!await configEntryRepository.ExistsAsync(projectName, request.EnvironmentName, request.Key, cancellationToken))
            return new ListConfigEntryVersionsResult(false, null, "Config entry not found");

        var versions = await configEntryRepository.ListVersionsAsync(projectName, request.EnvironmentName, request.Key, cancellationToken);
        return new ListConfigEntryVersionsResult(true, versions.Select(ConfigEntryMapping.ToDto).ToList(), null);
    }
}
