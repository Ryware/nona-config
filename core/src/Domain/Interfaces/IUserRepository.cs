using Nona.Domain.Entities;

namespace Nona.Domain.Interfaces;

public interface IUserRepository
{
    Task<User?> GetAsync(string email, CancellationToken ct = default);

    Task<IReadOnlyList<User>> ListAsync(CancellationToken ct = default);

    Task<bool> ExistsAsync(string email, CancellationToken ct = default);

    Task<bool> ExistsAnyAsync(CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
    Task UpdateAsync(User user, CancellationToken ct = default);
    Task<bool> DeleteAsync(string email, CancellationToken ct = default);

}
