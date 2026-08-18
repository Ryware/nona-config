using Nona.Domain.Entities;
using Nona.Domain.Interfaces;
using System.Collections.Concurrent;

namespace Nona.Infrastructure.Repositories.InMemory;

public sealed class InMemoryApiKeyRepository : IApiKeyRepository
{
    private readonly ConcurrentDictionary<long, ApiKey> _apiKeys = new();
    private readonly Func<IProjectRepository> _getProjectRepository;
    private long _nextId = 1;

    public InMemoryApiKeyRepository(IProjectRepository projectRepository)
        : this(() => projectRepository)
    {
    }

    public InMemoryApiKeyRepository(Func<IProjectRepository> getProjectRepository)
    {
        _getProjectRepository = getProjectRepository;
    }

    public Task<ApiKey?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        _apiKeys.TryGetValue(id, out var apiKey);
        return Task.FromResult(apiKey);
    }

    public async Task<ApiKeyAuthenticationResult?> GetByKeyAsync(string key, CancellationToken ct = default)
    {
        var apiKey = _apiKeys.Values.FirstOrDefault(k => k.Key == key);
        if (apiKey is null)
            return null;

        var project = await _getProjectRepository().GetByNameAsync(apiKey.Project, ct);
        if (project is null)
            return null;

        return new ApiKeyAuthenticationResult(project, apiKey.Scope, apiKey.Environment);
    }

    public Task<IReadOnlyList<ApiKey>> ListByProjectAsync(string projectName, CancellationToken ct = default)
    {
        var apiKeys = _apiKeys.Values
            .Where(k => string.Equals(k.Project, projectName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(k => k.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Task.FromResult<IReadOnlyList<ApiKey>>(apiKeys);
    }

    public Task AddAsync(ApiKey apiKey, CancellationToken ct = default)
    {
        if (apiKey.Id == 0)
            apiKey.Id = Interlocked.Increment(ref _nextId);

        _apiKeys[apiKey.Id] = apiKey;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(long id, CancellationToken ct = default)
    {
        _apiKeys.TryRemove(id, out _);
        return Task.CompletedTask;
    }

    internal void RenameEnvironment(string projectName, string currentName, string newName)
    {
        foreach (var apiKey in _apiKeys.Values)
        {
            if (string.Equals(apiKey.Project, projectName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(apiKey.Environment, currentName, StringComparison.OrdinalIgnoreCase))
            {
                apiKey.Environment = newName;
            }
        }
    }

    internal void RenameProject(string currentName, string newName)
    {
        foreach (var apiKey in _apiKeys.Values)
        {
            if (string.Equals(apiKey.Project, currentName, StringComparison.OrdinalIgnoreCase))
            {
                apiKey.Project = newName;
            }
        }
    }
}
