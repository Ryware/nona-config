using Nona.Domain.Entities;
using Nona.Domain.Interfaces;
using System.Collections.Concurrent;

namespace Nona.Infrastructure.Repositories.InMemory;

public sealed class InMemoryAuditLogRepository : IAuditLogRepository
{
    private readonly ConcurrentQueue<AuditLogEntry> _entries = new();
    private long _nextId;

    public Task AddAsync(AuditLogEntry entry, CancellationToken ct = default)
    {
        if (entry.Id == 0)
        {
            entry.Id = Interlocked.Increment(ref _nextId);
        }

        _entries.Enqueue(entry);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AuditLogEntry>> ListAsync(CancellationToken ct = default)
    {
        var entries = _entries
            .OrderByDescending(entry => entry.CreatedAt)
            .ThenByDescending(entry => entry.Id)
            .ToList();

        return Task.FromResult<IReadOnlyList<AuditLogEntry>>(entries);
    }
}
