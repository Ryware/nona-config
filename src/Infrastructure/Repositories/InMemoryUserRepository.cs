using Nona.Domain.Entities;
using Nona.Domain.Interfaces;
using System.Collections.Concurrent;

namespace Nona.Infrastructure.Repositories;

public class InMemoryUserRepository : IUserRepository
{
    private readonly ConcurrentDictionary<string, User> _users = new(StringComparer.OrdinalIgnoreCase);

    public Task<User?> GetAsync(string username, CancellationToken ct = default)
    {
        _users.TryGetValue(username, out var user);
        return Task.FromResult(user);
    }

    public Task<IReadOnlyList<User>> ListAsync(CancellationToken ct = default)
    {
        var users = _users.Values.ToList();
        return Task.FromResult<IReadOnlyList<User>>(users);
    }

    public Task<bool> ExistsAsync(string username, CancellationToken ct = default)
    {
        return Task.FromResult(_users.ContainsKey(username));
    }

    public Task AddAsync(User user, CancellationToken ct = default)
    {
        _users.TryAdd(user.Username, user);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(User user, CancellationToken ct = default)
    {
        _users[user.Username] = user;
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(string username, CancellationToken ct = default)
    {
        return Task.FromResult(_users.TryRemove(username, out _));
    }
}
