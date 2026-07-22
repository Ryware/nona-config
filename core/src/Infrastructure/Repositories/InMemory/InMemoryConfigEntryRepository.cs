using Nona.Domain;
using Nona.Domain.Entities;
using Nona.Domain.Interfaces;
using System.Collections.Concurrent;

namespace Nona.Infrastructure.Repositories.InMemory;

public class InMemoryConfigEntryRepository : IConfigEntryRepository
{
    private readonly ConcurrentDictionary<string, ConfigEntry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, List<ConfigEntryVersion>> _versions = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _versionGate = new();

    private static string GetKey(string projectName, string environmentName, string key) => $"{projectName}:{environmentName}:{key}";

    public Task<ConfigEntry?> GetAsync(string projectName, string environmentName, string key, CancellationToken ct = default)
    {
        _entries.TryGetValue(GetKey(projectName, environmentName, key), out var entry);
        return Task.FromResult(entry);
    }

    public Task<ConfigEntry?> AddVersionAsync(ConfigEntry entry, string actor, CancellationToken ct = default)
    {
        ConfigEntryKey.ThrowIfInvalid(entry.Key, nameof(entry));

        lock (_versionGate)
        {
            return Task.FromResult<ConfigEntry?>(AddVersionCore(entry, actor));
        }
    }

    public Task<IReadOnlyList<ConfigEntryVersion>> ListVersionsAsync(string projectName, string environmentName, string key, CancellationToken ct = default)
    {
        var storageKey = GetKey(projectName, environmentName, key);
        lock (_versionGate)
        {
            if (!_versions.TryGetValue(storageKey, out var versions))
            {
                return Task.FromResult<IReadOnlyList<ConfigEntryVersion>>([]);
            }

            return Task.FromResult<IReadOnlyList<ConfigEntryVersion>>(
                versions.OrderByDescending(version => version.Version).ToList());
        }
    }

    public Task<ConfigEntryVersion?> GetVersionAsync(string projectName, string environmentName, string key, int version, CancellationToken ct = default)
    {
        var storageKey = GetKey(projectName, environmentName, key);
        lock (_versionGate)
        {
            var entryVersion = _versions.TryGetValue(storageKey, out var versions)
                ? versions.FirstOrDefault(candidate => candidate.Version == version)
                : null;
            return Task.FromResult(entryVersion);
        }
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
        return AddVersionAsync(entry, "System", ct);
    }

    public Task UpdateAsync(ConfigEntry entry, CancellationToken ct = default)
    {
        return AddVersionAsync(entry, "System", ct);
    }

    public Task UpsertAsync(ConfigEntry entry, CancellationToken ct = default)
    {
        return AddVersionAsync(entry, "System", ct);
    }

    public Task DeleteAsync(string projectName, string environmentName, string key, CancellationToken ct = default)
    {
        var storageKey = GetKey(projectName, environmentName, key);
        _entries.TryRemove(storageKey, out _);
        _versions.TryRemove(storageKey, out _);
        return Task.CompletedTask;
    }

    public Task DeleteManyAsync(string projectName, string environmentName, IEnumerable<string> keys, CancellationToken ct = default)
    {
        foreach (var key in keys)
        {
            var storageKey = GetKey(projectName, environmentName, key);
            _entries.TryRemove(storageKey, out _);
            _versions.TryRemove(storageKey, out _);
        }
        return Task.CompletedTask;
    }

    public Task<int> CountAsync(CancellationToken ct = default)
    {
        return Task.FromResult(_entries.Count);
    }

    private ConfigEntry AddVersionCore(ConfigEntry entry, string actor)
    {
        var storageKey = GetKey(entry.Project, entry.Environment, entry.Key);
        _entries.TryGetValue(storageKey, out var existingEntry);

        var versions = _versions.GetOrAdd(storageKey, _ => []);
        var nextVersion = versions.Count == 0 ? 1 : versions.Max(version => version.Version) + 1;
        var versionTimestamp = entry.UpdatedAt;
        var createdAt = existingEntry?.CreatedAt ?? entry.CreatedAt;
        var normalizedActor = string.IsNullOrWhiteSpace(actor) ? "System" : actor;

        versions.Add(new ConfigEntryVersion
        {
            Project = entry.Project,
            Environment = entry.Environment,
            Key = entry.Key,
            Version = nextVersion,
            Value = entry.Value,
            ContentType = entry.ContentType,
            Scope = entry.Scope,
            CreatedAt = versionTimestamp,
            Actor = normalizedActor
        });

        var current = new ConfigEntry
        {
            Project = entry.Project,
            Environment = entry.Environment,
            Key = entry.Key,
            Value = entry.Value,
            ContentType = entry.ContentType,
            Scope = entry.Scope,
            ActiveVersion = nextVersion,
            CreatedAt = createdAt,
            UpdatedAt = versionTimestamp
        };

        _entries[storageKey] = current;
        return current;
    }
}
