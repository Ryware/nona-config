using Nona.Domain.Entities;
using Nona.Domain.Interfaces;
using System.Collections.Concurrent;

namespace Nona.Infrastructure.Repositories.InMemory;

public class InMemoryProjectMemberRepository : IProjectMemberRepository
{
    private readonly ConcurrentDictionary<string, ProjectMember> _members = new(StringComparer.OrdinalIgnoreCase);

    private static string GetKey(string username, string projectName) => $"{username}:{projectName}";

    public Task<ProjectMember?> GetAsync(string username, string projectName, CancellationToken ct = default)
    {
        _members.TryGetValue(GetKey(username, projectName), out var member);
        return Task.FromResult(member);
    }

    public Task<IReadOnlyList<ProjectMember>> ListByUserAsync(string username, CancellationToken ct = default)
    {
        var members = _members.Values
            .Where(m => m.Username.Equals(username, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return Task.FromResult<IReadOnlyList<ProjectMember>>(members);
    }

    public Task<IReadOnlyList<ProjectMember>> ListByProjectAsync(string projectName, CancellationToken ct = default)
    {
        var members = _members.Values
            .Where(m => m.ProjectName.Equals(projectName, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return Task.FromResult<IReadOnlyList<ProjectMember>>(members);
    }

    public Task<bool> ExistsAsync(string username, string projectName, CancellationToken ct = default)
    {
        return Task.FromResult(_members.ContainsKey(GetKey(username, projectName)));
    }

    public Task AddAsync(ProjectMember member, CancellationToken ct = default)
    {
        _members.TryAdd(GetKey(member.Username, member.ProjectName), member);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(ProjectMember member, CancellationToken ct = default)
    {
        _members[GetKey(member.Username, member.ProjectName)] = member;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string username, string projectName, CancellationToken ct = default)
    {
        _members.TryRemove(GetKey(username, projectName), out _);
        return Task.CompletedTask;
    }

    public Task DeleteByUserAsync(string username, CancellationToken ct = default)
    {
        var keysToRemove = _members.Keys
            .Where(k => k.StartsWith($"{username}:", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var key in keysToRemove)
        {
            _members.TryRemove(key, out _);
        }
        return Task.CompletedTask;
    }

    public Task DeleteByProjectAsync(string projectName, CancellationToken ct = default)
    {
        var keysToRemove = _members.Keys
            .Where(k => k.EndsWith($":{projectName}", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var key in keysToRemove)
        {
            _members.TryRemove(key, out _);
        }
        return Task.CompletedTask;
    }
}
