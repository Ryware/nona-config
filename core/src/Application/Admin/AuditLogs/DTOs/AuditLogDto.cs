using Nona.Domain.Entities;

namespace Nona.Application.Admin.AuditLogs.DTOs;

public record AuditLogDto(
    long Id,
    string Actor,
    bool ActorIsSystem,
    string ActionKind,
    string Action,
    string Target,
    string? Project,
    string? Environment,
    DateTime CreatedAt)
{
    public static AuditLogDto FromEntry(AuditLogEntry entry)
    {
        return new AuditLogDto(
            entry.Id,
            entry.Actor,
            entry.ActorIsSystem,
            entry.ActionKind.ToString().ToLowerInvariant(),
            entry.Action,
            entry.Target,
            entry.Project,
            entry.Environment,
            entry.CreatedAt);
    }
}
