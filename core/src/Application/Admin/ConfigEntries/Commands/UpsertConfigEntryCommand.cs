using Mediator;
using Nona.Application.Admin.ConfigEntries;
using Nona.Application.Admin.ConfigEntries.DTOs;
using Nona.Application.Admin.Projects;
using Nona.Application.Common;
using Nona.Application.Common.Interfaces;
using Nona.Domain;
using Nona.Domain.Entities;
using Nona.Domain.Enums;
using Nona.Domain.Interfaces;

namespace Nona.Application.Admin.ConfigEntries.Commands;

public record UpsertConfigEntryRequest(
    string Value,
    string? ContentType,
    string? Scope,
    string? Description = null,
    string? Unit = null);
public record UpsertConfigEntryCommand(
    string ProjectId,
    string EnvironmentName,
    string Key,
    string Value,
    string? ContentType,
    string? Scope,
    string? Description = null,
    string? Unit = null) : IRequest<UpsertConfigEntryResult>;

public record UpsertConfigEntryResult(
    bool Success,
    ConfigEntryDto? ConfigEntry,
    string? Error,
    string? ErrorCode = null);

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
        if (!ConfigEntryKey.IsValid(request.Key))
            return new UpsertConfigEntryResult(false, null, ConfigEntryKey.ValidationError);

        var project = await ProjectResolution.ResolveProjectAsync(projectRepository, request.ProjectId, cancellationToken);
        if (project is null)
            return new UpsertConfigEntryResult(false, null, "Project not found");

        var projectName = project.Name;
        if (!await projectAccessService.HasEditAccessAsync(projectName, cancellationToken))
            return new UpsertConfigEntryResult(
                false,
                null,
                "Access denied",
                AuthorizationErrorCodes.AccessDenied);

        if (!await environmentRepository.ExistsAsync(projectName, request.EnvironmentName, cancellationToken))
            return new UpsertConfigEntryResult(false, null, "Environment not found");

        var scope = EnumExtensions.ParseKeyScope(request.Scope);
        if (scope is null && request.Scope is not null)
            return new UpsertConfigEntryResult(false, null, "Invalid scope. Must be 'client', 'server', or 'all'");

        var existingEntry = await configEntryRepository.GetAsync(projectName, request.EnvironmentName, request.Key, cancellationToken);
        if (existingEntry is null && !ConfigEntryKey.IsValidHierarchy(request.Key))
            return new UpsertConfigEntryResult(false, null, ConfigEntryKey.HierarchyValidationError);

        var contentType = ConfigEntryContentTypes.Resolve(request.ContentType, existingEntry?.ContentType, request.Value, out var contentTypeError);
        if (contentTypeError is not null)
            return new UpsertConfigEntryResult(false, null, contentTypeError);

        var normalizedDescription = ConfigEntryMetadata.NormalizeDescription(request.Description);
        var normalizedUnit = ConfigEntryMetadata.NormalizeUnit(request.Unit);

        if (normalizedDescription?.Length > ConfigEntryMetadata.MaxDescriptionLength)
            return new UpsertConfigEntryResult(
                false,
                null,
                $"Description must be {ConfigEntryMetadata.MaxDescriptionLength} characters or fewer.");

        if (normalizedUnit?.Length > ConfigEntryMetadata.MaxUnitLength)
            return new UpsertConfigEntryResult(
                false,
                null,
                $"Unit must be {ConfigEntryMetadata.MaxUnitLength} characters or fewer.");

        if (normalizedUnit is not null && contentType is not "number")
            return new UpsertConfigEntryResult(false, null, "Unit is only supported for number parameters.");

        var description = request.Description is null
            ? existingEntry?.Description
            : normalizedDescription;
        var unit = contentType == "number"
            ? request.Unit is null
                ? existingEntry?.Unit
                : normalizedUnit
            : null;

        var now = dateTime.NowUtc;
        var action = existingEntry is null ? "Created Key" : "Updated Key";
        var actionKind = existingEntry is null ? AuditActionKind.Create : AuditActionKind.Update;
        var entry = new ConfigEntry
        {
            Project = projectName,
            Environment = request.EnvironmentName,
            Key = request.Key,
            Value = request.Value,
            ContentType = contentType,
            Description = description,
            Unit = unit,
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
                actionKind,
                action,
                savedEntry.Key,
                project: savedEntry.Project,
                environment: savedEntry.Environment,
                cancellationToken: cancellationToken);
        }

        return new UpsertConfigEntryResult(true, ConfigEntryMapping.ToDto(savedEntry), null);
    }
}
