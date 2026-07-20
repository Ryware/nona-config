using Mediator;
using Nona.Application.Admin.ConfigEntries;
using Nona.Application.Admin.ConfigEntries.DTOs;
using Nona.Application.Admin.Projects;
using Nona.Application.Common;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Entities;
using Nona.Domain.Enums;
using Nona.Domain.Interfaces;

namespace Nona.Application.Admin.ConfigEntries.Commands;

public record UpsertConfigEntryRequest(string Value, string? ContentType, string? Scope);
public record UpsertConfigEntryCommand(string ProjectId, string EnvironmentName, string Key, string Value, string? ContentType, string? Scope) : IRequest<UpsertConfigEntryResult>;

public record UpsertConfigEntryResult(bool Success, ConfigEntryDto? ConfigEntry, string? Error);

public class UpsertConfigEntryCommandHandler(
    IProjectRepository projectRepository,
    IEnvironmentRepository environmentRepository,
    IConfigEntryRepository configEntryRepository,
    IProjectAccessService projectAccessService,
    IDateTime dateTime,
    IAuditLogService? auditLogService = null,
    ICurrentUserService? currentUserService = null)
    : IRequestHandler<UpsertConfigEntryCommand, UpsertConfigEntryResult>
{
    public async ValueTask<UpsertConfigEntryResult> Handle(UpsertConfigEntryCommand request, CancellationToken cancellationToken)
    {
        var project = await ProjectResolution.ResolveProjectAsync(projectRepository, request.ProjectId, cancellationToken);
        if (project is null)
            return new UpsertConfigEntryResult(false, null, "Project not found");

        var projectName = project.Name;
        if (!await projectAccessService.HasEditAccessAsync(projectName, cancellationToken))
            return new UpsertConfigEntryResult(false, null, "Access denied");

        if (!await environmentRepository.ExistsAsync(projectName, request.EnvironmentName, cancellationToken))
            return new UpsertConfigEntryResult(false, null, "Environment not found");

        var scope = EnumExtensions.ParseKeyScope(request.Scope);
        if (scope is null && request.Scope is not null)
            return new UpsertConfigEntryResult(false, null, "Invalid scope. Must be 'client', 'server', or 'all'");

        var existingEntry = await configEntryRepository.GetAsync(projectName, request.EnvironmentName, request.Key, cancellationToken);
        var contentType = ConfigEntryContentTypes.Resolve(request.ContentType, existingEntry?.ContentType, request.Value, out var contentTypeError);
        if (contentTypeError is not null)
            return new UpsertConfigEntryResult(false, null, contentTypeError);

        var now = dateTime.NowUtc;
        var action = existingEntry is null ? "Created Key" : "Updated Key";
        var entry = new ConfigEntry
        {
            Project = projectName,
            Environment = request.EnvironmentName,
            Key = request.Key,
            Value = request.Value,
            ContentType = contentType,
            Scope = scope ?? existingEntry?.Scope ?? KeyScope.All,
            CreatedAt = existingEntry?.CreatedAt ?? now,
            UpdatedAt = now
        };

        var savedEntry = await configEntryRepository.AddVersionAsync(entry, currentUserService.ResolveActor(), cancellationToken);
        if (savedEntry is null)
            return new UpsertConfigEntryResult(false, null, "Config entry could not be saved");

        if (auditLogService is not null)
        {
            await auditLogService.WriteAsync(
                action,
                savedEntry.Key,
                project: savedEntry.Project,
                environment: savedEntry.Environment,
                cancellationToken: cancellationToken);
        }

        return new UpsertConfigEntryResult(true, ConfigEntryMapping.ToDto(savedEntry), null);
    }

}
