using Nona.Domain.Entities;
using Nona.Domain.Interfaces;
using System.Collections.Concurrent;

namespace Nona.Infrastructure.Repositories.InMemory;

public class InMemoryEnvironmentRepository : IEnvironmentRepository
{
    private readonly ConcurrentDictionary<string, ProjectEnvironment> _environments = new(StringComparer.OrdinalIgnoreCase);

    private static string GetKey(string projectName, string environmentName) => $"{projectName}:{environmentName}";

    public Task<ProjectEnvironment?> GetAsync(string projectName, string environmentName, CancellationToken ct = default)
    {
        _environments.TryGetValue(GetKey(projectName, environmentName), out var environment);
        return Task.FromResult(environment);
    }

    public Task<IReadOnlyList<ProjectEnvironment>> ListByProjectAsync(string projectName, CancellationToken ct = default)
    {
        var environments = _environments.Values
            .Where(e => e.Project.Equals(projectName, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return Task.FromResult<IReadOnlyList<ProjectEnvironment>>(environments);
    }

    public Task<bool> ExistsAsync(string projectName, string environmentName, CancellationToken ct = default)
    {
        return Task.FromResult(_environments.ContainsKey(GetKey(projectName, environmentName)));
    }

    public Task AddAsync(ProjectEnvironment environment, CancellationToken ct = default)
    {
        _environments.TryAdd(GetKey(environment.Project, environment.Name), environment);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(ProjectEnvironment environment, CancellationToken ct = default)
    {
        _environments[GetKey(environment.Project, environment.Name)] = environment;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string projectName, string environmentName, CancellationToken ct = default)
    {
        _environments.TryRemove(GetKey(projectName, environmentName), out _);
        return Task.CompletedTask;
    }
}
