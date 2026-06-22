using MediatR;
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
    IAuditLogService? auditLogService = null)
    : IRequestHandler<UpsertConfigEntryCommand, UpsertConfigEntryResult>
{
    public async Task<UpsertConfigEntryResult> Handle(UpsertConfigEntryCommand request, CancellationToken cancellationToken)
    {
        var project = await ProjectResolution.ResolveProjectAsync(projectRepository, request.ProjectId, cancellationToken);
        if (project is null)
            return new UpsertConfigEntryResult(false, null, "Project not found");

        var projectName = project.Name;
        if (!await projectAccessService.HasEditAccessAsync(projectName, cancellationToken))
            return new UpsertConfigEntryResult(false, null, "Access denied");

        if (!await environmentRepository.ExistsAsync(projectName, request.EnvironmentName, cancellationToken))
            return new UpsertConfigEntryResult(false, null, "Environment not found");

        var scope = ParseScope(request.Scope);
        if (scope is null && request.Scope is not null)
            return new UpsertConfigEntryResult(false, null, "Invalid scope. Must be 'client', 'server', or 'all'");

        var existingEntry = await configEntryRepository.GetAsync(projectName, request.EnvironmentName, request.Key, cancellationToken);
        var contentType = ResolveContentType(request.ContentType, existingEntry, request.Value, out var contentTypeError);
        if (contentTypeError is not null)
            return new UpsertConfigEntryResult(false, null, contentTypeError);

        var now = dateTime.NowUtc;
        var action = existingEntry is null ? "Created Key" : "Updated Key";

        ConfigEntry entry;
        if (existingEntry is not null)
        {
            existingEntry.Value = request.Value;
            existingEntry.ContentType = contentType;
            existingEntry.Scope = scope ?? existingEntry.Scope;
            existingEntry.UpdatedAt = now;
            await configEntryRepository.UpdateAsync(existingEntry, cancellationToken);
            entry = existingEntry;
        }
        else
        {
            entry = new ConfigEntry
            {
                Project = projectName,
                Environment = request.EnvironmentName,
                Key = request.Key,
                Value = request.Value,
                ContentType = contentType,
                Scope = scope ?? KeyScope.All,
                CreatedAt = now,
                UpdatedAt = now
            };
            await configEntryRepository.AddAsync(entry, cancellationToken);
        }

        if (auditLogService is not null)
        {
            await auditLogService.WriteAsync(
                action,
                entry.Key,
                project: entry.Project,
                environment: entry.Environment,
                cancellationToken: cancellationToken);
        }

        var dto = new ConfigEntryDto(
            entry.Project,
            entry.Environment,
            entry.Key,
            entry.Value,
            entry.ContentType,
            entry.Scope.ToApiString(),
            entry.CreatedAt,
            entry.UpdatedAt);

        return new UpsertConfigEntryResult(true, dto, null);
    }

    private static KeyScope? ParseScope(string? scope)
    {
        if (scope is null)
            return null;

        return scope.ToLowerInvariant() switch
        {
            "client" => KeyScope.Frontend,
            "server" => KeyScope.Backend,
            "all" => KeyScope.All,
            _ => null
        };
    }

    private static string ResolveContentType(
        string? requestedContentType,
        ConfigEntry? existingEntry,
        string value,
        out string? error)
    {
        error = null;

        var normalizedRequested = ConfigEntryContentTypes.Normalize(requestedContentType);
        if (!string.IsNullOrWhiteSpace(requestedContentType) && normalizedRequested is null)
        {
            error = $"Content type must be one of: {string.Join(", ", ConfigEntryContentTypes.LogicalTypes)}.";
            return ConfigEntryContentTypes.Text;
        }

        var contentType = normalizedRequested
            ?? ConfigEntryContentTypes.Normalize(existingEntry?.ContentType)
            ?? ConfigEntryContentTypes.Infer(value);

        if (!ConfigEntryContentTypes.IsValidValue(value, contentType, out var validationError))
            error = validationError;

        return contentType;
    }
}
