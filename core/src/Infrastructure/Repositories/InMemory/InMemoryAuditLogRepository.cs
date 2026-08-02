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

    public Task<AuditLogPageResult> ListAsync(AuditLogPageRequest request, CancellationToken ct = default)
    {
        IEnumerable<AuditLogEntry> query = _entries;
        var filter = request.Filter;

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();
            query = query.Where(entry =>
                entry.Actor.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                entry.Target.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (entry.Project?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        if (!string.IsNullOrWhiteSpace(filter.Action))
        {
            query = query.Where(entry => string.Equals(
                entry.Action,
                filter.Action,
                StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(filter.Environment))
        {
            query = query.Where(entry => string.Equals(
                entry.Environment,
                filter.Environment,
                StringComparison.OrdinalIgnoreCase));
        }

        if (filter.CreatedFrom is { } createdFrom)
        {
            query = query.Where(entry => entry.CreatedAt >= createdFrom);
        }

        if (filter.CreatedToExclusive is { } createdToExclusive)
        {
            query = query.Where(entry => entry.CreatedAt < createdToExclusive);
        }

        var filtered = query
            .OrderByDescending(entry => entry.CreatedAt)
            .ThenByDescending(entry => entry.Id)
            .ToList();

        var actions = _entries
            .Select(entry => entry.Action)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var environments = _entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Environment))
            .Select(entry => entry.Environment!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var result = new AuditLogPageResult(
            filtered.Skip(checked((int)Math.Min(request.Offset, int.MaxValue))).Take(request.Limit).ToList(),
            filtered.Count,
            actions,
            environments);

        return Task.FromResult(result);
    }
}
