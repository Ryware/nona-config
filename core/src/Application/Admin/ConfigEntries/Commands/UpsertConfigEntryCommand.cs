using MediatR;
using Nona.Application.Admin.ConfigEntries.DTOs;
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
    IDateTime dateTime)
    : IRequestHandler<UpsertConfigEntryCommand, UpsertConfigEntryResult>
{
    public async Task<UpsertConfigEntryResult> Handle(UpsertConfigEntryCommand request, CancellationToken cancellationToken)
    {
        if (!await projectRepository.ExistsAsync(request.ProjectId, cancellationToken))
            return new UpsertConfigEntryResult(false, null, "Project not found");

        if (!await projectAccessService.HasAccessAsync(request.ProjectId, cancellationToken))
            return new UpsertConfigEntryResult(false, null, "Access denied");

        if (!await environmentRepository.ExistsAsync(request.ProjectId, request.EnvironmentName, cancellationToken))
            return new UpsertConfigEntryResult(false, null, "Environment not found");

        var scope = ParseScope(request.Scope);
        if (scope is null && request.Scope is not null)
            return new UpsertConfigEntryResult(false, null, "Invalid scope. Must be 'client', 'server', or 'all'");

        var now = dateTime.NowUtc;
        var existingEntry = await configEntryRepository.GetAsync(request.ProjectId, request.EnvironmentName, request.Key, cancellationToken);

        ConfigEntry entry;
        if (existingEntry is not null)
        {
            existingEntry.Value = request.Value;
            existingEntry.ContentType = request.ContentType ?? existingEntry.ContentType;
            existingEntry.Scope = scope ?? existingEntry.Scope;
            existingEntry.UpdatedAt = now;
            await configEntryRepository.UpdateAsync(existingEntry, cancellationToken);
            entry = existingEntry;
        }
        else
        {
            entry = new ConfigEntry
            {
                Project = request.ProjectId,
                Environment = request.EnvironmentName,
                Key = request.Key,
                Value = request.Value,
                ContentType = request.ContentType ?? "string",
                Scope = scope ?? KeyScope.All,
                CreatedAt = now,
                UpdatedAt = now
            };
            await configEntryRepository.AddAsync(entry, cancellationToken);
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
}
