using Nona.Domain.Entities;
using Nona.Domain.Interfaces;
using System.Collections.Concurrent;

namespace Nona.Infrastructure.Repositories.InMemory;

public class InMemoryUserRepository : IUserRepository
{
    private readonly ConcurrentDictionary<string, User> _users = new(StringComparer.OrdinalIgnoreCase);

    public Task<User?> GetAsync(string email, CancellationToken ct = default)
    {
        _users.TryGetValue(email, out var user);
        return Task.FromResult(user);
    }

    public Task<IReadOnlyList<User>> ListAsync(CancellationToken ct = default)
    {
        var users = _users.Values.ToList();
        return Task.FromResult<IReadOnlyList<User>>(users);
    }

    public Task<bool> ExistsAsync(string email, CancellationToken ct = default)
    {
        return Task.FromResult(_users.ContainsKey(email));
    }

    public Task<bool> ExistsAnyAsync(CancellationToken ct = default)
    {
        return Task.FromResult(_users.Any());
    }

    public Task AddAsync(User user, CancellationToken ct = default)
    {
        _users.TryAdd(user.Email, user);
        return Task.CompletedTask;
    }


    public Task UpdateAsync(User user, CancellationToken ct = default)
    {
        _users[user.Email] = user;
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(string email, CancellationToken ct = default)
    {
        return Task.FromResult(_users.TryRemove(email, out _));
    }
}
