using Nona.Domain.Entities;
using Nona.Domain.Interfaces;
using System.Collections.Concurrent;

namespace Nona.Infrastructure.Repositories.InMemory;

public sealed class InMemoryParameterShareLinkRepository : IParameterShareLinkRepository
{
    private readonly ConcurrentDictionary<long, ParameterShareLink> _shareLinks = new();
    private long _nextId;

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
        if (shareLink.Id == 0)
        {
            shareLink.Id = Interlocked.Increment(ref _nextId);
        }

        _shareLinks[shareLink.Id] = shareLink;
        return Task.CompletedTask;
    }

    public Task RevokeAsync(long id, DateTime revokedAt, CancellationToken ct = default)
    {
        if (_shareLinks.TryGetValue(id, out var shareLink))
        {
            shareLink.RevokedAt = revokedAt;
        }

        return Task.CompletedTask;
    }
}
