using Nona.Domain.Entities;
using Nona.Domain.Interfaces;
using System.Collections.Concurrent;

namespace Nona.Infrastructure.Repositories.InMemory;

public sealed class InMemoryExternalIdentityRepository : IExternalIdentityRepository
{
    private readonly ConcurrentDictionary<string, ExternalIdentity> _identities = new(StringComparer.OrdinalIgnoreCase);
    private long _nextId;

    public Task<ExternalIdentity?> GetAsync(string provider, string issuer, string subject, CancellationToken ct = default)
    {
        _identities.TryGetValue(CreateKey(provider, issuer, subject), out var identity);
        return Task.FromResult(identity);
    }

    public Task<IReadOnlyList<ExternalIdentity>> ListAsync(CancellationToken ct = default)
    {
        var identities = _identities.Values
            .OrderBy(identity => identity.Provider, StringComparer.OrdinalIgnoreCase)
            .ThenBy(identity => identity.Issuer, StringComparer.OrdinalIgnoreCase)
            .ThenBy(identity => identity.Subject, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Task.FromResult<IReadOnlyList<ExternalIdentity>>(identities);
    }

    public Task<IReadOnlyList<ExternalIdentity>> ListByUserEmailAsync(string userEmail, CancellationToken ct = default)
    {
        var identities = _identities.Values
            .Where(identity => string.Equals(identity.UserEmail, userEmail, StringComparison.OrdinalIgnoreCase))
            .OrderBy(identity => identity.Provider, StringComparer.OrdinalIgnoreCase)
            .ThenBy(identity => identity.Subject, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Task.FromResult<IReadOnlyList<ExternalIdentity>>(identities);
    }

    public Task AddAsync(ExternalIdentity identity, CancellationToken ct = default)
    {
        if (identity.Id == 0)
        {
            identity.Id = Interlocked.Increment(ref _nextId);
        }

        _identities[CreateKey(identity.Provider, identity.Issuer, identity.Subject)] = identity;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(ExternalIdentity identity, CancellationToken ct = default)
    {
        _identities[CreateKey(identity.Provider, identity.Issuer, identity.Subject)] = identity;
        return Task.CompletedTask;
    }

    public Task<int> CountAsync(CancellationToken ct = default)
    {
        return Task.FromResult(_identities.Count);
    }

    private static string CreateKey(string provider, string issuer, string subject)
    {
        return $"{provider}::{issuer}::{subject}";
    }
}
