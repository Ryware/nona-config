using Nona.Domain.Entities;
using Nona.Domain.Enums;
using Nona.Domain.Interfaces;

namespace Nona.Infrastructure.Services;

public sealed class AuditLogService(
    IAuditLogRepository auditLogRepository,
    ICurrentUserService currentUserService,
    IDateTime dateTime) : IAuditLogService
{
    public Task WriteAsync(
        AuditActionKind actionKind,
        string action,
        string target,
        string? project = null,
        string? environment = null,
        CancellationToken cancellationToken = default)
    {
        var actor = currentUserService.Username;
        var isSystem = string.IsNullOrWhiteSpace(actor);

        return WriteAsAsync(
            isSystem ? "System" : actor!,
            isSystem,
            actionKind,
            action,
            target,
            project,
            environment,
            cancellationToken);
    }

    public Task WriteAsAsync(
        string actor,
        bool actorIsSystem,
        AuditActionKind actionKind,
        string action,
        string target,
        string? project = null,
        string? environment = null,
        CancellationToken cancellationToken = default)
    {
        return auditLogRepository.AddAsync(
            new AuditLogEntry
            {
                Actor = string.IsNullOrWhiteSpace(actor) ? "System" : actor,
                ActorIsSystem = actorIsSystem,
                ActionKind = actionKind,
                Action = action,
                Target = target,
                Project = project,
                Environment = environment,
                CreatedAt = dateTime.NowUtc
            },
            cancellationToken);
    }
}
