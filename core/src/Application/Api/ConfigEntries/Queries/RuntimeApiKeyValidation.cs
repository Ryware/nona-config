using Nona.Domain.Interfaces;

namespace Nona.Application.Api.ConfigEntries.Queries;

internal static class RuntimeApiKeyValidation
{
    // Check after the protected read: deletion/recreation must not let an already-started
    // request return data from a replacement resource using a now-deleted credential.
    public static async Task<bool> IsCurrentAsync(IApiKeyRepository repository, string hash,
        ApiKeyAuthenticationResult authorized, CancellationToken ct)
    {
        var current = await repository.GetByKeyHashAsync(hash, ct);
        return current is not null
            && current.Project.Id == authorized.Project.Id
            && current.Project.CreatedAt == authorized.Project.CreatedAt
            && string.Equals(current.Project.Name, authorized.Project.Name, StringComparison.OrdinalIgnoreCase)
            && current.Scope == authorized.Scope
            && string.Equals(current.Environment, authorized.Environment, StringComparison.OrdinalIgnoreCase);
    }
}
