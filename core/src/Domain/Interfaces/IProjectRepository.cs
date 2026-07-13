using Nona.Domain.Entities;
namespace Nona.Domain.Interfaces;

public interface IProjectRepository
{
    Task<Project?> GetByNameAsync(string name, CancellationToken ct = default);

    Task<IReadOnlyList<Project>> ListAsync(CancellationToken ct = default);

    Task<bool> ExistsAsync(string name, CancellationToken ct = default);

    Task AddAsync(Project project, CancellationToken ct = default);

    Task UpdateAsync(Project project, CancellationToken ct = default);

    Task DeleteAsync(string name, CancellationToken ct = default);

    Task<int> CountAsync(CancellationToken ct = default);

}
