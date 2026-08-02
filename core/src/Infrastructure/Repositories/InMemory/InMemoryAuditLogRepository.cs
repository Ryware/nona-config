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
        var filtered = ApplyFilter(_entries, request.Filter)
            .OrderByDescending(entry => entry.CreatedAt)
            .ThenByDescending(entry => entry.Id)
            .ToList();

        var actions = _entries
            .Select(entry => entry.Action)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var environments = _entries
            .Select(entry => string.IsNullOrWhiteSpace(entry.Environment)
                ? AuditLogFilter.GlobalScopeEnvironment
                : entry.Environment!)
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

    public Task<IReadOnlyList<AuditLogEntry>> ListBatchAsync(
        AuditLogBatchRequest request,
        CancellationToken ct = default)
    {
        var query = ApplyFilter(_entries, request.Filter);
        if (request.BeforeCreatedAt is { } beforeCreatedAt && request.BeforeId is { } beforeId)
        {
            query = query.Where(entry =>
                entry.CreatedAt < beforeCreatedAt ||
                (entry.CreatedAt == beforeCreatedAt && entry.Id < beforeId));
        }

        var entries = query
            .OrderByDescending(entry => entry.CreatedAt)
            .ThenByDescending(entry => entry.Id)
            .Take(request.Limit)
            .ToList();

        return Task.FromResult<IReadOnlyList<AuditLogEntry>>(entries);
    }

    private static IEnumerable<AuditLogEntry> ApplyFilter(
        IEnumerable<AuditLogEntry> entries,
        AuditLogFilter filter)
    {
        var query = entries;

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
            var environment = filter.Environment.Trim();
            query = AuditLogFilter.IsGlobalScopeEnvironment(environment)
                ? query.Where(entry => string.IsNullOrWhiteSpace(entry.Environment))
                : query.Where(entry => string.Equals(
                    entry.Environment,
                    environment,
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

        return query;
    }
}
