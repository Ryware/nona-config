using Nona.Domain.Enums;

namespace Nona.Application.Common.Interfaces;

public interface IAuditLogService
{
    Task WriteAsync(
        AuditActionKind actionKind,
        string action,
        string target,
        string? project = null,
        string? environment = null,
        CancellationToken cancellationToken = default);

    Task WriteAsAsync(
        string actor,
        bool actorIsSystem,
        AuditActionKind actionKind,
        string action,
        string target,
        string? project = null,
        string? environment = null,
        CancellationToken cancellationToken = default);
}
