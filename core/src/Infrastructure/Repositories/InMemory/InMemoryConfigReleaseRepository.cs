using Nona.Domain.Entities;
using Nona.Domain.Interfaces;
using System.Collections.Concurrent;

namespace Nona.Infrastructure.Repositories.InMemory;

public class InMemoryConfigReleaseRepository : IConfigReleaseRepository
{
    private readonly ConcurrentDictionary<string, ConfigRelease> _releases = new(StringComparer.OrdinalIgnoreCase);

    private static string GetKey(string projectName, string environmentName, string version)
        => $"{projectName}:{environmentName}:{version}";

    public Task<ConfigRelease?> GetAsync(string projectName, string environmentName, string version, CancellationToken ct = default)
    {
        _releases.TryGetValue(GetKey(projectName, environmentName, version), out var release);
        return Task.FromResult(release);
    }

    public Task<ConfigRelease?> GetLatestPatchAsync(string projectName, string environmentName, int major, int minor, CancellationToken ct = default)
    {
        var release = _releases.Values
            .Where(candidate =>
                candidate.Project.Equals(projectName, StringComparison.OrdinalIgnoreCase)
                && candidate.Environment.Equals(environmentName, StringComparison.OrdinalIgnoreCase)
                && candidate.Major == major
                && candidate.Minor == minor)
            .OrderByDescending(candidate => candidate.Patch)
            .FirstOrDefault();

        return Task.FromResult(release);
    }

    public Task<IReadOnlyList<ConfigRelease>> ListAsync(string projectName, string environmentName, CancellationToken ct = default)
    {
        var releases = _releases.Values
            .Where(candidate =>
                candidate.Project.Equals(projectName, StringComparison.OrdinalIgnoreCase)
                && candidate.Environment.Equals(environmentName, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(candidate => candidate.Major)
            .ThenByDescending(candidate => candidate.Minor)
            .ThenByDescending(candidate => candidate.Patch)
            .ToList();

        return Task.FromResult<IReadOnlyList<ConfigRelease>>(releases);
    }

    public Task<bool> ExistsAsync(string projectName, string environmentName, string version, CancellationToken ct = default)
    {
        return Task.FromResult(_releases.ContainsKey(GetKey(projectName, environmentName, version)));
    }

    public Task<bool> AddAsync(ConfigRelease release, CancellationToken ct = default)
    {
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

        return Task.FromResult(_releases.TryAdd(
            GetKey(release.Project, release.Environment, release.Version),
            releaseWithCount));
    }

    public Task<bool> DeleteAsync(string projectName, string environmentName, string version, CancellationToken ct = default)
    {
        return Task.FromResult(_releases.TryRemove(GetKey(projectName, environmentName, version), out _));
    }

    public Task DeleteByEnvironmentAsync(string projectName, string environmentName, CancellationToken ct = default)
    {
        foreach (var release in _releases.Values)
        {
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
        foreach (var release in _releases.Values)
        {
            if (release.Project.Equals(projectName, StringComparison.OrdinalIgnoreCase))
            {
                _releases.TryRemove(GetKey(release.Project, release.Environment, release.Version), out _);
            }
        }

        return Task.CompletedTask;
    }
}
