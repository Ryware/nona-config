using Nona.Domain.Entities;
using Nona.Domain.Enums;

namespace Nona.Domain.Interfaces;

public record ApiKeyAuthenticationResult(Project Project, KeyScope Scope, string? Environment);

public interface IApiKeyRepository
{
    Task<ApiKey?> GetByIdAsync(long id, CancellationToken ct = default);

    Task<ApiKeyAuthenticationResult?> GetByKeyAsync(string key, CancellationToken ct = default);

    Task<IReadOnlyList<ApiKey>> ListByProjectAsync(string projectName, CancellationToken ct = default);

    Task AddAsync(ApiKey apiKey, CancellationToken ct = default);

    Task DeleteAsync(long id, CancellationToken ct = default);
}
