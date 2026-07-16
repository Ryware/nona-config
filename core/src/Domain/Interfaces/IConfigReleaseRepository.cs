using Nona.Domain.Entities;

namespace Nona.Domain.Interfaces;

public interface IConfigReleaseRepository
{
    Task<ConfigRelease?> GetAsync(string projectName, string environmentName, string version, CancellationToken ct = default);

    Task<ConfigRelease?> GetLatestPatchAsync(string projectName, string environmentName, int major, int minor, CancellationToken ct = default);

    Task<IReadOnlyList<ConfigRelease>> ListAsync(string projectName, string environmentName, CancellationToken ct = default);

    Task<bool> ExistsAsync(string projectName, string environmentName, string version, CancellationToken ct = default);

    Task<bool> AddAsync(ConfigRelease release, CancellationToken ct = default);

    Task<bool> DeleteAsync(string projectName, string environmentName, string version, CancellationToken ct = default);

    Task DeleteByEnvironmentAsync(string projectName, string environmentName, CancellationToken ct = default);

    Task DeleteByProjectAsync(string projectName, CancellationToken ct = default);
}
