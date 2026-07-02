using Nona.Domain.Entities;

namespace Nona.Domain.Interfaces;

public interface IParameterShareLinkRepository
{
    Task<ParameterShareLink?> GetByIdAsync(long id, CancellationToken ct = default);

    Task<ParameterShareLink?> GetByTokenHashAsync(string tokenHash, CancellationToken ct = default);

    Task<IReadOnlyList<ParameterShareLink>> ListByConfigEntryAsync(
        string projectName,
        string environmentName,
        string key,
        CancellationToken ct = default);

    Task AddAsync(ParameterShareLink shareLink, CancellationToken ct = default);

    Task RevokeAsync(long id, DateTime revokedAt, CancellationToken ct = default);
}
