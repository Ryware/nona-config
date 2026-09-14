using Nona.Domain.Entities;
using Nona.Domain.Interfaces;
using System.Collections.Concurrent;

namespace Nona.Infrastructure.Repositories.InMemory;

public sealed class InMemoryParameterShareLinkRepository : IParameterShareLinkRepository
{
    private readonly ConcurrentDictionary<long, ParameterShareLink> _shareLinks = new();
    private long _nextId;
    internal object SyncRoot => InMemoryRepositoryGate.SyncRoot;

    public Task<ParameterShareLink?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        _shareLinks.TryGetValue(id, out var shareLink);
        return Task.FromResult(shareLink);
    }

    public Task<ParameterShareLink?> GetByTokenHashAsync(string tokenHash, CancellationToken ct = default)
    {
        var shareLink = _shareLinks.Values.FirstOrDefault(candidate =>
            string.Equals(candidate.TokenHash, tokenHash, StringComparison.Ordinal));

        return Task.FromResult(shareLink);
    }

    public Task<IReadOnlyList<ParameterShareLink>> ListByConfigEntryAsync(
        string projectName,
        string environmentName,
        string key,
        CancellationToken ct = default)
    {
        var shareLinks = _shareLinks.Values
            .Where(candidate =>
                string.Equals(candidate.Project, projectName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(candidate.Environment, environmentName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(candidate.Key, key, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(candidate => candidate.CreatedAt)
            .ThenByDescending(candidate => candidate.Id)
            .ToList();

        return Task.FromResult<IReadOnlyList<ParameterShareLink>>(shareLinks);
    }

    public Task AddAsync(ParameterShareLink shareLink, CancellationToken ct = default)
    {
        lock (SyncRoot)
        {
            if (shareLink.Id == 0) shareLink.Id = Interlocked.Increment(ref _nextId);
            _shareLinks[shareLink.Id] = shareLink;
        }
        return Task.CompletedTask;
    }

    public Task RevokeAsync(long id, DateTime revokedAt, CancellationToken ct = default)
    {
        lock (SyncRoot)
            if (_shareLinks.TryGetValue(id, out var shareLink))
            {
                shareLink.RevokedAt = revokedAt;
            }

        return Task.CompletedTask;
    }

    internal bool IsActive(ParameterShareLink link, DateTime now, bool requireEdit = false)
        => _shareLinks.TryGetValue(link.Id, out var current)
            && current.TokenHash == link.TokenHash && (!requireEdit || current.CanEdit) && current.RevokedAt is null && current.ExpiresAt > now
            && string.Equals(current.Project, link.Project, StringComparison.OrdinalIgnoreCase)
            && string.Equals(current.Environment, link.Environment, StringComparison.OrdinalIgnoreCase)
            && string.Equals(current.Key, link.Key, StringComparison.OrdinalIgnoreCase);

    internal void DeleteProject(string projectName)
    {
        lock (SyncRoot)
            foreach (var item in _shareLinks.Values)
            {
                if (string.Equals(item.Project, projectName, StringComparison.OrdinalIgnoreCase))
                    _shareLinks.TryRemove(item.Id, out _);
            }
    }

    internal void DeleteEnvironment(string projectName, string environmentName, string? key = null)
    {
        lock (SyncRoot)
            foreach (var item in _shareLinks.Values)
                if (string.Equals(item.Project, projectName, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(item.Environment, environmentName, StringComparison.OrdinalIgnoreCase)
                    && (key is null || string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase)))
                    _shareLinks.TryRemove(item.Id, out _);
    }

    internal void RenameEnvironment(string projectName, string currentName, string newName)
    {
        foreach (var shareLink in _shareLinks.Values)
        {
            if (string.Equals(shareLink.Project, projectName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(shareLink.Environment, currentName, StringComparison.OrdinalIgnoreCase))
            {
                shareLink.Environment = newName;
            }
        }
    }

    internal void RenameProject(string currentName, string newName)
    {
        foreach (var shareLink in _shareLinks.Values)
        {
            if (string.Equals(shareLink.Project, currentName, StringComparison.OrdinalIgnoreCase))
            {
                shareLink.Project = newName;
            }
        }
    }
}
