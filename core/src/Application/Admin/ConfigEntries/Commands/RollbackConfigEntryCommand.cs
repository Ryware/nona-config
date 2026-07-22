using Mediator;
using Nona.Application.Admin.ConfigEntries.DTOs;
using Nona.Application.Admin.Projects;
using Nona.Application.Common;
using Nona.Application.Common.Interfaces;
using Nona.Domain;
using Nona.Domain.Entities;
using Nona.Domain.Interfaces;

namespace Nona.Application.Admin.ConfigEntries.Commands;

public record RollbackConfigEntryRequest(int Version);

public record RollbackConfigEntryCommand(string ProjectId, string EnvironmentName, string Key, int Version) : IRequest<RollbackConfigEntryResult>;

public record RollbackConfigEntryResult(bool Success, ConfigEntryDto? ConfigEntry, string? Error);

public class RollbackConfigEntryCommandHandler(
    IProjectRepository projectRepository,
    IEnvironmentRepository environmentRepository,
    IConfigEntryRepository configEntryRepository,
    IProjectAccessService projectAccessService,
    IDateTime dateTime,
    IAuditLogService? auditLogService = null,
    ICurrentUserService? currentUserService = null)
    : IRequestHandler<RollbackConfigEntryCommand, RollbackConfigEntryResult>
{
    public async ValueTask<RollbackConfigEntryResult> Handle(RollbackConfigEntryCommand request, CancellationToken cancellationToken)
    {
        if (!ConfigEntryKey.IsValid(request.Key))
            return new RollbackConfigEntryResult(false, null, ConfigEntryKey.ValidationError);

        if (request.Version <= 0)
            return new RollbackConfigEntryResult(false, null, "Version must be greater than zero");

        var project = await ProjectResolution.ResolveProjectAsync(projectRepository, request.ProjectId, cancellationToken);
        if (project is null)
            return new RollbackConfigEntryResult(false, null, "Project not found");

        var projectName = project.Name;
        if (!await projectAccessService.HasEditAccessAsync(projectName, cancellationToken))
            return new RollbackConfigEntryResult(false, null, "Access denied");

        if (!await environmentRepository.ExistsAsync(projectName, request.EnvironmentName, cancellationToken))
            return new RollbackConfigEntryResult(false, null, "Environment not found");

        var currentEntry = await configEntryRepository.GetAsync(projectName, request.EnvironmentName, request.Key, cancellationToken);
        if (currentEntry is null)
            return new RollbackConfigEntryResult(false, null, "Config entry not found");

        var targetVersion = await configEntryRepository.GetVersionAsync(projectName, request.EnvironmentName, request.Key, request.Version, cancellationToken);
        if (targetVersion is null)
            return new RollbackConfigEntryResult(false, null, "Version not found");

        var now = dateTime.NowUtc;
        var rollbackEntry = new ConfigEntry
        {
            Project = projectName,
            Environment = request.EnvironmentName,
            Key = request.Key,
            Value = targetVersion.Value,
            ContentType = targetVersion.ContentType,
            Scope = targetVersion.Scope,
            CreatedAt = currentEntry.CreatedAt,
            UpdatedAt = now
        };

        var savedEntry = await configEntryRepository.AddVersionAsync(rollbackEntry, currentUserService.ResolveActor(), cancellationToken);
        if (savedEntry is null)
            return new RollbackConfigEntryResult(false, null, "Config entry could not be saved");

        if (auditLogService is not null)
        {
            await auditLogService.WriteAsync(
                $"Rolled Back Key to v{request.Version}",
                savedEntry.Key,
                project: savedEntry.Project,
                environment: savedEntry.Environment,
                cancellationToken: cancellationToken);
        }

        return new RollbackConfigEntryResult(true, ConfigEntryMapping.ToDto(savedEntry), null);
    }

}
