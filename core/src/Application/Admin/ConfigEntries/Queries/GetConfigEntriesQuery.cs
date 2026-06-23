using Mediator;
using Nona.Application.Admin.ConfigEntries;
using Nona.Application.Admin.ConfigEntries.DTOs;
using Nona.Application.Admin.Projects;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Interfaces;

namespace Nona.Application.Admin.ConfigEntries.Queries;

public record GetConfigEntriesQuery(string ProjectId, string EnvironmentName) : IRequest<GetConfigEntriesResult>;

public record GetConfigEntriesResult(bool Success, List<ConfigEntryDto>? ConfigEntries, string? Error);

public class GetConfigEntriesQueryHandler(
    IProjectRepository projectRepository,
    IEnvironmentRepository environmentRepository,
    IConfigEntryRepository configEntryRepository,
    IProjectAccessService projectAccessService)
    : IRequestHandler<GetConfigEntriesQuery, GetConfigEntriesResult>
{
    public async ValueTask<GetConfigEntriesResult> Handle(GetConfigEntriesQuery request, CancellationToken cancellationToken)
    {
        var project = await ProjectResolution.ResolveProjectAsync(projectRepository, request.ProjectId, cancellationToken);
        if (project is null)
            return new GetConfigEntriesResult(false, null, "Project not found");

        var projectName = project.Name;
        if (!await projectAccessService.HasViewAccessAsync(projectName, cancellationToken))
            return new GetConfigEntriesResult(false, null, "Access denied");

        if (!await environmentRepository.ExistsAsync(projectName, request.EnvironmentName, cancellationToken))
            return new GetConfigEntriesResult(false, null, "Environment not found");

        var configEntries = await configEntryRepository.ListAsync(projectName, request.EnvironmentName, cancellationToken);

        var dtos = configEntries.Select(ConfigEntryMapping.ToDto).ToList();

        return new GetConfigEntriesResult(true, dtos, null);
    }
}
