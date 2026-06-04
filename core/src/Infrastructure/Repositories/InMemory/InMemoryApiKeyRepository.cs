using Nona.Domain.Entities;
using Nona.Domain.Interfaces;
using System.Collections.Concurrent;

namespace Nona.Infrastructure.Repositories.InMemory;

public sealed class InMemoryApiKeyRepository(IProjectRepository projectRepository) : IApiKeyRepository
{
    private readonly ConcurrentDictionary<long, ApiKey> _apiKeys = new();
    private long _nextId = 1;

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

        var project = await projectRepository.GetByNameAsync(apiKey.Project, ct);
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
}
