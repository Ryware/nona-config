using MediatR;
using Nona.Application.Admin.ConfigEntries.DTOs;
using Nona.Application.Common;
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
    public async Task<GetConfigEntryResult> Handle(GetConfigEntryQuery request, CancellationToken cancellationToken)
    {
        if (!await projectRepository.ExistsAsync(request.ProjectId, cancellationToken))
            return new GetConfigEntryResult(false, null, "Project not found");

        if (!await projectAccessService.HasAccessAsync(request.ProjectId, cancellationToken))
            return new GetConfigEntryResult(false, null, "Access denied");

        if (!await environmentRepository.ExistsAsync(request.ProjectId, request.EnvironmentName, cancellationToken))
            return new GetConfigEntryResult(false, null, "Environment not found");

        var configEntry = await configEntryRepository.GetAsync(request.ProjectId, request.EnvironmentName, request.Key, cancellationToken);
        if (configEntry is null)
            return new GetConfigEntryResult(false, null, "Config entry not found");

        var dto = new ConfigEntryDto(
            configEntry.Project,
            configEntry.Environment,
            configEntry.Key,
            configEntry.Value,
            configEntry.ContentType,
            configEntry.Scope.ToApiString(),
            configEntry.CreatedAt,
            configEntry.UpdatedAt);

        return new GetConfigEntryResult(true, dto, null);
    }
}
