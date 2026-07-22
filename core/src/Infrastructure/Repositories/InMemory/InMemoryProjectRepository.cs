using Nona.Domain.Entities;
using Nona.Domain.Interfaces;
using System.Collections.Concurrent;

namespace Nona.Infrastructure.Repositories.InMemory;

public class InMemoryProjectRepository : IProjectRepository
{
    private readonly ConcurrentDictionary<string, Project> _projects = new(StringComparer.OrdinalIgnoreCase);
    private readonly InMemoryEnvironmentRepository? _environmentRepository;
    private readonly InMemoryConfigEntryRepository? _configEntryRepository;
    private readonly InMemoryConfigReleaseRepository? _configReleaseRepository;
    private readonly InMemoryApiKeyRepository? _apiKeyRepository;
    private readonly InMemoryParameterShareLinkRepository? _parameterShareLinkRepository;
    private readonly InMemoryProjectMemberRepository? _projectMemberRepository;
    private readonly object _renameGate = new();
    private long _nextId = 1;

    public InMemoryProjectRepository()
    {
    }

    public InMemoryProjectRepository(
        InMemoryEnvironmentRepository environmentRepository,
        InMemoryConfigEntryRepository configEntryRepository,
        InMemoryConfigReleaseRepository configReleaseRepository,
        InMemoryApiKeyRepository apiKeyRepository,
        InMemoryParameterShareLinkRepository parameterShareLinkRepository,
        InMemoryProjectMemberRepository projectMemberRepository)
    {
        _environmentRepository = environmentRepository;
        _configEntryRepository = configEntryRepository;
        _configReleaseRepository = configReleaseRepository;
        _apiKeyRepository = apiKeyRepository;
        _parameterShareLinkRepository = parameterShareLinkRepository;
        _projectMemberRepository = projectMemberRepository;
    }

    public Task<Project?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        _projects.TryGetValue(name, out var project);
        return Task.FromResult(project);
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
        if (project.Id == 0)
            project.Id = Interlocked.Increment(ref _nextId);
        _projects.TryAdd(project.Name, project);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Project project, CancellationToken ct = default)
    {
        _projects[project.Name] = project;
        return Task.CompletedTask;
    }

    public Task RenameAsync(
        string currentName,
        string newName,
        DateTime updatedAt,
        CancellationToken ct = default)
    {
        lock (_renameGate)
        {
            RenameProject(currentName, newName, updatedAt);
            _environmentRepository?.RenameProject(currentName, newName);
            _configEntryRepository?.RenameProject(currentName, newName);
            _configReleaseRepository?.RenameProject(currentName, newName);
            _apiKeyRepository?.RenameProject(currentName, newName);
            _parameterShareLinkRepository?.RenameProject(currentName, newName);
            _projectMemberRepository?.RenameProject(currentName, newName);
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(string name, CancellationToken ct = default)
    {
        _projects.TryRemove(name, out _);
        return Task.CompletedTask;
    }

    public Task<int> CountAsync(CancellationToken ct = default)
    {
        return Task.FromResult(_projects.Count);
    }

    private void RenameProject(string currentName, string newName, DateTime updatedAt)
    {
        if (!_projects.TryRemove(currentName, out var project))
        {
            return;
        }

        _projects[newName] = new Project
        {
            Id = project.Id,
            Name = newName,
            UrlSlug = project.UrlSlug,
            Environments = project.Environments.ToList(),
            CreatedAt = project.CreatedAt,
            UpdatedAt = updatedAt
        };
    }
}
