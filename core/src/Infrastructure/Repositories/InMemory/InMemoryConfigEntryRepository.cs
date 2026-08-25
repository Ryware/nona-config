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
        => ListAsync(projectName, environmentName, prefix: null, ct);

    public Task<IReadOnlyList<ConfigEntry>> ListAsync(
        string projectName,
        string environmentName,
        string? prefix,
        CancellationToken ct = default)
    {
        var entries = _entries.Values
            .Where(e => e.Project.Equals(projectName, StringComparison.OrdinalIgnoreCase)
                     && e.Environment.Equals(environmentName, StringComparison.OrdinalIgnoreCase)
                     && (string.IsNullOrEmpty(prefix)
                         || e.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(e => e.Key, StringComparer.Ordinal)
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

    internal void RenameEnvironment(string projectName, string currentName, string newName)
    {
        lock (_versionGate)
        {
            var matchingEntries = _entries.Values
                .Where(entry =>
                    entry.Project.Equals(projectName, StringComparison.OrdinalIgnoreCase)
                    && entry.Environment.Equals(currentName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var entry in matchingEntries)
            {
                var currentKey = GetKey(projectName, currentName, entry.Key);
                var newKey = GetKey(projectName, newName, entry.Key);
                _entries.TryRemove(currentKey, out _);
                _entries[newKey] = CloneEntry(entry, projectName, newName);

                if (_versions.TryRemove(currentKey, out var versions))
                {
                    _versions[newKey] = versions
                        .Select(version => CloneVersion(version, projectName, newName))
                        .ToList();
                }
            }
        }
    }

    internal void RenameProject(string currentName, string newName)
    {
        lock (_versionGate)
        {
            var matchingEntries = _entries.Values
                .Where(entry => entry.Project.Equals(currentName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var entry in matchingEntries)
            {
                var currentKey = GetKey(currentName, entry.Environment, entry.Key);
                var newKey = GetKey(newName, entry.Environment, entry.Key);
                _entries.TryRemove(currentKey, out _);
                _entries[newKey] = CloneEntry(entry, newName, entry.Environment);

                if (_versions.TryRemove(currentKey, out var versions))
                {
                    _versions[newKey] = versions
                        .Select(version => CloneVersion(version, newName, version.Environment))
                        .ToList();
                }
            }
        }
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
            Description = entry.Description,
            Unit = entry.Unit,
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
            Description = entry.Description,
            Unit = entry.Unit,
            Scope = entry.Scope,
            ActiveVersion = nextVersion,
            CreatedAt = createdAt,
            UpdatedAt = versionTimestamp
        };

        _entries[storageKey] = current;
        return current;
    }

    private static ConfigEntry CloneEntry(ConfigEntry entry, string projectName, string environmentName)
    {
        return new ConfigEntry
        {
            Project = projectName,
            Environment = environmentName,
            Key = entry.Key,
            Value = entry.Value,
            ContentType = entry.ContentType,
            Description = entry.Description,
            Unit = entry.Unit,
            Scope = entry.Scope,
            ActiveVersion = entry.ActiveVersion,
            CreatedAt = entry.CreatedAt,
            UpdatedAt = entry.UpdatedAt
        };
    }

    private static ConfigEntryVersion CloneVersion(
        ConfigEntryVersion version,
        string projectName,
        string environmentName)
    {
        return new ConfigEntryVersion
        {
            Project = projectName,
            Environment = environmentName,
            Key = version.Key,
            Version = version.Version,
            Value = version.Value,
            ContentType = version.ContentType,
            Description = version.Description,
            Unit = version.Unit,
            Scope = version.Scope,
            CreatedAt = version.CreatedAt,
            Actor = version.Actor
        };
    }
}
