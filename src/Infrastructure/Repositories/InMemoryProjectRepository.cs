using Nona.Domain.Entities;
using Nona.Domain.Enums;
using Nona.Domain.Interfaces;
using System.Collections.Concurrent;

namespace Nona.Infrastructure.Repositories;

public class InMemoryProjectRepository : IProjectRepository
{
    private readonly ConcurrentDictionary<string, Project> _projects = new(StringComparer.OrdinalIgnoreCase);

    public Task<Project?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        _projects.TryGetValue(name, out var project);
        return Task.FromResult(project);
    }

    public Task<ApiKeyLookupResult?> GetByApiKeyAsync(string apiKey, CancellationToken ct = default)
    {
        foreach (var project in _projects.Values)
        {
            if (project.ServerApiKey == apiKey)
                return Task.FromResult<ApiKeyLookupResult?>(new ApiKeyLookupResult(project, KeyScope.Backend));

            if (project.ClientApiKey == apiKey)
                return Task.FromResult<ApiKeyLookupResult?>(new ApiKeyLookupResult(project, KeyScope.Frontend));
        }

        return Task.FromResult<ApiKeyLookupResult?>(null);
    }

    public Task<IReadOnlyList<Project>> ListAsync(CancellationToken ct = default)
    {
        var projects = _projects.Values.ToList();
        return Task.FromResult<IReadOnlyList<Project>>(projects);
    }

    public Task<bool> ExistsAsync(string name, CancellationToken ct = default)
    {
        return Task.FromResult(_projects.ContainsKey(name));
    }

    public Task AddAsync(Project project, CancellationToken ct = default)
    {
        _projects.TryAdd(project.Name, project);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Project project, CancellationToken ct = default)
    {
        _projects[project.Name] = project;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string name, CancellationToken ct = default)
    {
        _projects.TryRemove(name, out _);
        return Task.CompletedTask;
    }
}
