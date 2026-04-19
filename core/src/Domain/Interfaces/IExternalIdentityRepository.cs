using Nona.Domain.Entities;

namespace Nona.Domain.Interfaces;

public interface IExternalIdentityRepository
{
    Task<ExternalIdentity?> GetAsync(string provider, string issuer, string subject, CancellationToken ct = default);
    Task<IReadOnlyList<ExternalIdentity>> ListAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ExternalIdentity>> ListByUserEmailAsync(string userEmail, CancellationToken ct = default);
    Task AddAsync(ExternalIdentity identity, CancellationToken ct = default);
    Task UpdateAsync(ExternalIdentity identity, CancellationToken ct = default);
    Task<int> CountAsync(CancellationToken ct = default);
}
