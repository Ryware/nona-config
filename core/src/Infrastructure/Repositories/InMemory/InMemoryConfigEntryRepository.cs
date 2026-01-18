using Nona.Domain.Entities;
using Nona.Domain.Interfaces;
using System.Collections.Concurrent;

namespace Nona.Infrastructure.Repositories.InMemory;

public class InMemoryConfigEntryRepository : IConfigEntryRepository
{
    private readonly ConcurrentDictionary<string, ConfigEntry> _entries = new(StringComparer.OrdinalIgnoreCase);

    private static string GetKey(string projectName, string environmentName, string key) => $"{projectName}:{environmentName}:{key}";

    public Task<ConfigEntry?> GetAsync(string projectName, string environmentName, string key, CancellationToken ct = default)
    {
        _entries.TryGetValue(GetKey(projectName, environmentName, key), out var entry);
        return Task.FromResult(entry);
    }

    public Task<IReadOnlyList<ConfigEntry>> ListAsync(string projectName, string environmentName, CancellationToken ct = default)
    {
        var entries = _entries.Values
            .Where(e => e.Project.Equals(projectName, StringComparison.OrdinalIgnoreCase)
                     && e.Environment.Equals(environmentName, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return Task.FromResult<IReadOnlyList<ConfigEntry>>(entries);
    }

    public Task<IReadOnlyList<ConfigEntry>> ListByProjectAsync(string projectName, CancellationToken ct = default)
    {
        var entries = _entries.Values
            .Where(e => e.Project.Equals(projectName, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return Task.FromResult<IReadOnlyList<ConfigEntry>>(entries);
    }

    public Task<bool> ExistsAsync(string projectName, string environmentName, string key, CancellationToken ct = default)
    {
        return Task.FromResult(_entries.ContainsKey(GetKey(projectName, environmentName, key)));
    }

    public Task AddAsync(ConfigEntry entry, CancellationToken ct = default)
    {
        _entries.TryAdd(GetKey(entry.Project, entry.Environment, entry.Key), entry);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(ConfigEntry entry, CancellationToken ct = default)
    {
        _entries[GetKey(entry.Project, entry.Environment, entry.Key)] = entry;
        return Task.CompletedTask;
    }

    public Task UpsertAsync(ConfigEntry entry, CancellationToken ct = default)
    {
        _entries[GetKey(entry.Project, entry.Environment, entry.Key)] = entry;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string projectName, string environmentName, string key, CancellationToken ct = default)
    {
        _entries.TryRemove(GetKey(projectName, environmentName, key), out _);
        return Task.CompletedTask;
    }

    public Task DeleteManyAsync(string projectName, string environmentName, IEnumerable<string> keys, CancellationToken ct = default)
    {
        foreach (var key in keys)
        {
            _entries.TryRemove(GetKey(projectName, environmentName, key), out _);
        }
        return Task.CompletedTask;
    }
}
