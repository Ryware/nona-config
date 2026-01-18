using Nona.Domain.Entities;

namespace Nona.Domain.Interfaces;

public interface IEnvironmentRepository
{
    Task<ProjectEnvironment?> GetAsync(string projectName, string environmentName, CancellationToken ct = default);

    Task<IReadOnlyList<ProjectEnvironment>> ListByProjectAsync(string projectName, CancellationToken ct = default);

    Task<bool> ExistsAsync(string projectName, string environmentName, CancellationToken ct = default);


    Task AddAsync(ProjectEnvironment environment, CancellationToken ct = default);

    Task UpdateAsync(ProjectEnvironment environment, CancellationToken ct = default);


    Task DeleteAsync(string projectName, string environmentName, CancellationToken ct = default);
}
