using Nona.Domain.Entities;
using Nona.Domain.Interfaces;

namespace Nona.Infrastructure.Services;

public sealed class AuditLogService(
    IAuditLogRepository auditLogRepository,
    ICurrentUserService currentUserService,
    IDateTime dateTime) : IAuditLogService
{
    public Task WriteAsync(
        string action,
        string target,
        string? project = null,
        string? environment = null,
        CancellationToken cancellationToken = default)
    {
        var actor = currentUserService.Username;
        var isSystem = string.IsNullOrWhiteSpace(actor);

        return auditLogRepository.AddAsync(
            new AuditLogEntry
            {
                Actor = isSystem ? "System" : actor!,
                ActorIsSystem = isSystem,
                Action = action,
                Target = target,
                Project = project,
                Environment = environment,
                CreatedAt = dateTime.NowUtc
            },
            cancellationToken);
    }
}
