using Nona.Domain.Entities;

namespace Nona.Domain.Interfaces;

public interface IConfigEntryRepository
{
    Task<ConfigEntry?> GetAsync(string projectName, string environmentName, string key, CancellationToken ct = default);

    Task<IReadOnlyList<ConfigEntry>> ListAsync(string projectName, string environmentName, CancellationToken ct = default);

    Task<IReadOnlyList<ConfigEntry>> ListByProjectAsync(string projectName, CancellationToken ct = default);

    Task<bool> ExistsAsync(string projectName, string environmentName, string key, CancellationToken ct = default);


    Task AddAsync(ConfigEntry entry, CancellationToken ct = default);

    Task UpdateAsync(ConfigEntry entry, CancellationToken ct = default);

    Task UpsertAsync(ConfigEntry entry, CancellationToken ct = default);


    Task DeleteAsync(string projectName, string environmentName, string key, CancellationToken ct = default);

    Task DeleteManyAsync(string projectName, string environmentName, IEnumerable<string> keys, CancellationToken ct = default);
}
