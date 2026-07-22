using Nona.Domain;
using Nona.Domain.Entities;
using Nona.Domain.Enums;
using Nona.Domain.Interfaces;
using System.Collections.Concurrent;

namespace Nona.Infrastructure.Repositories.InMemory;

public class InMemoryConfigReleaseRepository : IConfigReleaseRepository
{
    private readonly ConcurrentDictionary<string, StoredRelease> _releases = new(StringComparer.OrdinalIgnoreCase);

    private static string GetKey(string projectName, string environmentName, string version)
        => $"{projectName}:{environmentName}:{version}";

    public Task<ConfigRelease?> GetMetadataAsync(
        string projectName,
        string environmentName,
        string version,
        CancellationToken ct = default)
    {
        _releases.TryGetValue(GetKey(projectName, environmentName, version), out var storedRelease);
        return Task.FromResult(storedRelease is null ? null : ToMetadata(storedRelease.Release));
    }

    public Task<ConfigRelease?> GetLatestPatchMetadataAsync(
        string projectName,
        string environmentName,
        int major,
        int minor,
        CancellationToken ct = default)
    {
        var storedRelease = FindLatestPatch(projectName, environmentName, major, minor);
        return Task.FromResult(storedRelease is null ? null : ToMetadata(storedRelease.Release));
    }

    public Task<ConfigRelease?> GetAsync(string projectName, string environmentName, string version, CancellationToken ct = default)
    {
        _releases.TryGetValue(GetKey(projectName, environmentName, version), out var storedRelease);
        return Task.FromResult(storedRelease?.Release);
    }

    public Task<ConfigRelease?> GetLatestPatchAsync(string projectName, string environmentName, int major, int minor, CancellationToken ct = default)
    {
        return Task.FromResult(FindLatestPatch(projectName, environmentName, major, minor)?.Release);
    }

    private StoredRelease? FindLatestPatch(string projectName, string environmentName, int major, int minor)
    {
        return _releases.Values
            .Where(candidate =>
                candidate.Release.Project.Equals(projectName, StringComparison.OrdinalIgnoreCase)
                && candidate.Release.Environment.Equals(environmentName, StringComparison.OrdinalIgnoreCase)
                && candidate.Release.Major == major
                && candidate.Release.Minor == minor)
            .OrderByDescending(candidate => candidate.Release.Patch)
            .FirstOrDefault();
    }

    public Task<ConfigReleaseEntryLookupResult> GetEntryAsync(
        string projectName,
        string environmentName,
        string version,
        string key,
        KeyScope requiredScope,
        CancellationToken ct = default)
    {
        if (!_releases.TryGetValue(GetKey(projectName, environmentName, version), out var storedRelease))
        {
            return Task.FromResult(new ConfigReleaseEntryLookupResult(false, null));
        }

        return Task.FromResult(CreateEntryLookup(storedRelease, key, requiredScope));
    }

    public Task<ConfigReleaseEntryLookupResult> GetLatestPatchEntryAsync(
        string projectName,
        string environmentName,
        int major,
        int minor,
        string key,
        KeyScope requiredScope,
        CancellationToken ct = default)
    {
        var storedRelease = FindLatestPatch(projectName, environmentName, major, minor);

        return Task.FromResult(storedRelease is null
            ? new ConfigReleaseEntryLookupResult(false, null)
            : CreateEntryLookup(storedRelease, key, requiredScope));
    }

    public Task<IReadOnlyList<ConfigRelease>> ListAsync(string projectName, string environmentName, CancellationToken ct = default)
    {
        var releases = _releases.Values
            .Select(storedRelease => storedRelease.Release)
            .Where(candidate =>
                candidate.Project.Equals(projectName, StringComparison.OrdinalIgnoreCase)
                && candidate.Environment.Equals(environmentName, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(candidate => candidate.Major)
            .ThenByDescending(candidate => candidate.Minor)
            .ThenByDescending(candidate => candidate.Patch)
            .ToList();

        return Task.FromResult<IReadOnlyList<ConfigRelease>>(releases);
    }

    public Task<IReadOnlyList<ConfigReleaseEntry>> ListEntriesAsync(
        string projectName,
        string environmentName,
        string version,
        KeyScope requiredScope,
        CancellationToken ct = default)
    {
        if (!_releases.TryGetValue(GetKey(projectName, environmentName, version), out var storedRelease))
        {
            return Task.FromResult<IReadOnlyList<ConfigReleaseEntry>>([]);
        }

        var entries = storedRelease.Release.Entries
            .Where(entry => (entry.Scope & requiredScope) != 0)
            .ToList();
        return Task.FromResult<IReadOnlyList<ConfigReleaseEntry>>(entries);
    }

    public Task<bool> ExistsAsync(string projectName, string environmentName, string version, CancellationToken ct = default)
    {
        return Task.FromResult(_releases.ContainsKey(GetKey(projectName, environmentName, version)));
    }

    public Task<bool> AddAsync(ConfigRelease release, CancellationToken ct = default)
    {
        var uniqueKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in release.Entries)
        {
            ConfigEntryKey.ThrowIfInvalid(entry.Key, nameof(release));
            if (!uniqueKeys.Add(entry.Key))
            {
                throw new ArgumentException("Release entries must have unique case-insensitive keys.", nameof(release));
            }
        }

        var releaseWithCount = new ConfigRelease
        {
            Project = release.Project,
            Environment = release.Environment,
            Version = release.Version,
            Major = release.Major,
            Minor = release.Minor,
            Patch = release.Patch,
            Entries = release.Entries.ToList(),
            EntryCount = release.Entries.Count,
            CreatedAt = release.CreatedAt,
            Actor = release.Actor
        };

        var entriesByKey = new Dictionary<string, ConfigReleaseEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in releaseWithCount.Entries)
        {
            entriesByKey.TryAdd(entry.Key, entry);
        }

        return Task.FromResult(_releases.TryAdd(
            GetKey(release.Project, release.Environment, release.Version),
            new StoredRelease(releaseWithCount, entriesByKey)));
    }

    public Task<bool> DeleteAsync(string projectName, string environmentName, string version, CancellationToken ct = default)
    {
        return Task.FromResult(_releases.TryRemove(GetKey(projectName, environmentName, version), out _));
    }

    public Task DeleteByEnvironmentAsync(string projectName, string environmentName, CancellationToken ct = default)
    {
        foreach (var storedRelease in _releases.Values)
        {
            var release = storedRelease.Release;
            if (release.Project.Equals(projectName, StringComparison.OrdinalIgnoreCase)
                && release.Environment.Equals(environmentName, StringComparison.OrdinalIgnoreCase))
            {
                _releases.TryRemove(GetKey(release.Project, release.Environment, release.Version), out _);
            }
        }

        return Task.CompletedTask;
    }

    public Task DeleteByProjectAsync(string projectName, CancellationToken ct = default)
    {
        foreach (var storedRelease in _releases.Values)
        {
            var release = storedRelease.Release;
            if (release.Project.Equals(projectName, StringComparison.OrdinalIgnoreCase))
            {
                _releases.TryRemove(GetKey(release.Project, release.Environment, release.Version), out _);
            }
        }

        return Task.CompletedTask;
    }

    private static ConfigRelease ToMetadata(ConfigRelease release)
    {
        return new ConfigRelease
        {
            Project = release.Project,
            Environment = release.Environment,
            Version = release.Version,
            Major = release.Major,
            Minor = release.Minor,
            Patch = release.Patch,
            EntryCount = release.EntryCount,
            CreatedAt = release.CreatedAt,
            Actor = release.Actor
        };
    }

    private static ConfigReleaseEntryLookupResult CreateEntryLookup(
        StoredRelease storedRelease,
        string key,
        KeyScope requiredScope)
    {
        storedRelease.EntriesByKey.TryGetValue(key, out var entry);
        if (entry is not null && (entry.Scope & requiredScope) == 0)
        {
            entry = null;
        }

        return new ConfigReleaseEntryLookupResult(true, entry);
    }

    private sealed record StoredRelease(
        ConfigRelease Release,
        IReadOnlyDictionary<string, ConfigReleaseEntry> EntriesByKey);
}
