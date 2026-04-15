using Nona.Domain.Entities;

namespace Nona.Domain.Interfaces;

public interface IAuditLogRepository
{
    Task AddAsync(AuditLogEntry entry, CancellationToken ct = default);

    Task<IReadOnlyList<AuditLogEntry>> ListAsync(CancellationToken ct = default);
}
