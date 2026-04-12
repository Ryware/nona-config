namespace Nona.Application.Admin.AuditLogs.DTOs;

public record AuditLogDto(
    long Id,
    string Actor,
    bool ActorIsSystem,
    string Action,
    string Target,
    string? Project,
    string? Environment,
    DateTime CreatedAt);
