using Nona.Domain.Entities;

namespace Nona.Domain.Interfaces;

public interface IAuditLogRepository
{
    Task AddAsync(AuditLogEntry entry, CancellationToken ct = default);

    Task<AuditLogPageResult> ListAsync(AuditLogPageRequest request, CancellationToken ct = default);
}
