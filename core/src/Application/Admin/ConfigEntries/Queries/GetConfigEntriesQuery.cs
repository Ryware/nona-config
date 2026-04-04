using MediatR;
using Nona.Application.Admin.ConfigEntries.DTOs;
using Nona.Application.Common;
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
    public async Task<GetConfigEntriesResult> Handle(GetConfigEntriesQuery request, CancellationToken cancellationToken)
    {
        if (!await projectRepository.ExistsAsync(request.ProjectId, cancellationToken))
            return new GetConfigEntriesResult(false, null, "Project not found");

        if (!await projectAccessService.HasAccessAsync(request.ProjectId, cancellationToken))
            return new GetConfigEntriesResult(false, null, "Access denied");

        if (!await environmentRepository.ExistsAsync(request.ProjectId, request.EnvironmentName, cancellationToken))
            return new GetConfigEntriesResult(false, null, "Environment not found");

        var configEntries = await configEntryRepository.ListAsync(request.ProjectId, request.EnvironmentName, cancellationToken);

        var dtos = configEntries.Select(e => new ConfigEntryDto(
            e.Project,
            e.Environment,
            e.Key,
            e.Value,
            e.ContentType,
            e.Scope.ToApiString(),
            e.CreatedAt,
            e.UpdatedAt)).ToList();

        return new GetConfigEntriesResult(true, dtos, null);
    }
}
