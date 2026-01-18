using Nona.Domain.Entities;

namespace Nona.Domain.Interfaces;

public interface IProjectMemberRepository
{
    Task<ProjectMember?> GetAsync(string username, string projectName, CancellationToken ct = default);

    Task<IReadOnlyList<ProjectMember>> ListByUserAsync(string username, CancellationToken ct = default);

    Task<IReadOnlyList<ProjectMember>> ListByProjectAsync(string projectName, CancellationToken ct = default);

    Task<bool> ExistsAsync(string username, string projectName, CancellationToken ct = default);

    Task AddAsync(ProjectMember member, CancellationToken ct = default);

    Task UpdateAsync(ProjectMember member, CancellationToken ct = default);

    Task DeleteAsync(string username, string projectName, CancellationToken ct = default);

    Task DeleteByUserAsync(string username, CancellationToken ct = default);

    Task DeleteByProjectAsync(string projectName, CancellationToken ct = default);
}
