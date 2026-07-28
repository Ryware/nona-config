using Nona.Domain.Entities;
using Nona.Domain.Interfaces;
using System.Collections.Concurrent;

namespace Nona.Infrastructure.Repositories.InMemory;

public class InMemoryEnvironmentRepository : IEnvironmentRepository
{
    private readonly ConcurrentDictionary<string, ProjectEnvironment> _environments = new(StringComparer.OrdinalIgnoreCase);
    private readonly InMemoryConfigEntryRepository? _configEntryRepository;
    private readonly InMemoryConfigReleaseRepository? _configReleaseRepository;
    private readonly InMemoryApiKeyRepository? _apiKeyRepository;
    private readonly InMemoryParameterShareLinkRepository? _parameterShareLinkRepository;
    private readonly object _renameGate = new();

    public InMemoryEnvironmentRepository()
    {
    }

    public InMemoryEnvironmentRepository(
        InMemoryConfigEntryRepository configEntryRepository,
        InMemoryConfigReleaseRepository configReleaseRepository,
        InMemoryApiKeyRepository apiKeyRepository,
        InMemoryParameterShareLinkRepository parameterShareLinkRepository)
    {
        _configEntryRepository = configEntryRepository;
        _configReleaseRepository = configReleaseRepository;
        _apiKeyRepository = apiKeyRepository;
        _parameterShareLinkRepository = parameterShareLinkRepository;
    }

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
        _environments.TryAdd(GetKey(environment.Project, environment.Name), Clone(environment));
        return Task.CompletedTask;
    }

    public Task UpdateAsync(ProjectEnvironment environment, CancellationToken ct = default)
    {
        _environments[GetKey(environment.Project, environment.Name)] = Clone(environment);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string projectName, string environmentName, CancellationToken ct = default)
    {
        _environments.TryRemove(GetKey(projectName, environmentName), out _);
        return Task.CompletedTask;
    }

    public Task RenameAsync(
        string projectName,
        string currentName,
        string newName,
        DateTime updatedAt,
        CancellationToken ct = default)
    {
        lock (_renameGate)
        {
            RenameEnvironment(projectName, currentName, newName, updatedAt);
            _configEntryRepository?.RenameEnvironment(projectName, currentName, newName);
            _configReleaseRepository?.RenameEnvironment(projectName, currentName, newName);
            _apiKeyRepository?.RenameEnvironment(projectName, currentName, newName);
            _parameterShareLinkRepository?.RenameEnvironment(projectName, currentName, newName);
        }

        return Task.CompletedTask;
    }

    private void RenameEnvironment(string projectName, string currentName, string newName, DateTime updatedAt)
    {
        if (!_environments.TryRemove(GetKey(projectName, currentName), out var environment))
        {
            return;
        }

        _environments[GetKey(projectName, newName)] = new ProjectEnvironment
        {
            Name = newName,
            Project = projectName,
            ConfigEntries = environment.ConfigEntries.ToList(),
            ActiveReleaseVersion = environment.ActiveReleaseVersion,
            CreatedAt = environment.CreatedAt,
            UpdatedAt = updatedAt
        };
    }

    internal void RenameProject(string currentName, string newName)
    {
        var matchingEnvironments = _environments.Values
            .Where(environment => environment.Project.Equals(currentName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var environment in matchingEnvironments)
        {
            _environments.TryRemove(GetKey(currentName, environment.Name), out _);
            _environments[GetKey(newName, environment.Name)] = new ProjectEnvironment
            {
                Name = environment.Name,
                Project = newName,
                ConfigEntries = environment.ConfigEntries.ToList(),
                ActiveReleaseVersion = environment.ActiveReleaseVersion,
                CreatedAt = environment.CreatedAt,
                UpdatedAt = environment.UpdatedAt
            };
        }
    }

    private static ProjectEnvironment Clone(ProjectEnvironment environment)
    {
        return new ProjectEnvironment
        {
            Name = environment.Name,
            Project = environment.Project,
            ConfigEntries = environment.ConfigEntries.ToList(),
            ActiveReleaseVersion = environment.ActiveReleaseVersion,
            CreatedAt = environment.CreatedAt,
            UpdatedAt = environment.UpdatedAt
        };
    }
}
