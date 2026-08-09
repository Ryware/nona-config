using Nona.Domain.Entities;
using Nona.Domain.Interfaces;
using System.Collections.Concurrent;

namespace Nona.Infrastructure.Repositories.InMemory;

public class InMemoryUserRepository : IUserRepository
{
    private readonly ConcurrentDictionary<string, User> _users = new(StringComparer.OrdinalIgnoreCase);
    private long _nextId = 1;

    public Task<User?> GetAsync(string email, CancellationToken ct = default)
    {
        _users.TryGetValue(email, out var user);
        return Task.FromResult(user);
    }

    public Task<User?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var user = _users.Values.FirstOrDefault(u => u.Id == id);
        return Task.FromResult(user);
    }

    public Task<User?> GetByInviteTokenHashAsync(string inviteTokenHash, CancellationToken ct = default)
    {
        var user = _users.Values.FirstOrDefault(candidate =>
            string.Equals(candidate.InviteTokenHash, inviteTokenHash, StringComparison.Ordinal));
        return Task.FromResult(user);
    }

    public Task<User?> GetByPasswordResetTokenHashAsync(string passwordResetTokenHash, CancellationToken ct = default)
    {
        var user = _users.Values.FirstOrDefault(candidate =>
            string.Equals(candidate.PasswordResetTokenHash, passwordResetTokenHash, StringComparison.Ordinal));
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
        if (user.Id == 0)
            user.Id = Interlocked.Increment(ref _nextId);
        _users.TryAdd(user.Email, user);
        return Task.CompletedTask;
    }


    public Task UpdateAsync(User user, CancellationToken ct = default)
    {
        _users[user.Email] = user;
        return Task.CompletedTask;
    }

    public Task<bool> TryResetPasswordAsync(
        string passwordResetTokenHash,
        DateTime nowUtc,
        string passwordHash,
        string passwordSalt,
        DateTime updatedAt,
        CancellationToken ct = default)
    {
        var user = _users.Values.FirstOrDefault(candidate =>
            string.Equals(candidate.PasswordResetTokenHash, passwordResetTokenHash, StringComparison.Ordinal));
        if (user is null)
            return Task.FromResult(false);

        lock (user)
        {
            if (!string.Equals(user.PasswordResetTokenHash, passwordResetTokenHash, StringComparison.Ordinal)
                || user.PasswordResetTokenExpiresAt is null
                || user.PasswordResetTokenExpiresAt <= nowUtc)
            {
                return Task.FromResult(false);
            }

            user.PasswordHash = passwordHash;
            user.PasswordSalt = passwordSalt;
            user.PasswordResetTokenHash = null;
            user.PasswordResetTokenExpiresAt = null;
            user.UpdatedAt = updatedAt;
            return Task.FromResult(true);
        }
    }

    public Task<bool> DeleteAsync(string email, CancellationToken ct = default)
    {
        return Task.FromResult(_users.TryRemove(email, out _));
    }

    public Task<int> CountAsync(CancellationToken ct = default)
    {
        return Task.FromResult(_users.Count);
    }
}
