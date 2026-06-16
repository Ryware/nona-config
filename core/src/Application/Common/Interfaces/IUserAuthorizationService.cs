using Nona.Domain.Entities;
using Nona.Domain.Interfaces;

namespace Nona.Application.Common.Interfaces;

public interface IUserAuthorizationService
{
    Task<User?> GetCurrentUserAsync(CancellationToken ct = default);
    Task<bool> CanManageUsersAsync(CancellationToken ct = default);
    Task<bool> HasGlobalProjectAccessAsync(CancellationToken ct = default);
}

public class UserAuthorizationService(
    ICurrentUserService currentUserService,
    IUserRepository userRepository) : IUserAuthorizationService
{
    public async Task<User?> GetCurrentUserAsync(CancellationToken ct = default)
    {
        var username = currentUserService.Username;
        if (string.IsNullOrWhiteSpace(username))
            return null;

        return await userRepository.GetAsync(username, ct);
    }

    public async Task<bool> CanManageUsersAsync(CancellationToken ct = default)
    {
        var user = await GetCurrentUserAsync(ct);
        return user?.IsAdmin == true || user?.Role == UserRole.Editor;
    }

    public async Task<bool> HasGlobalProjectAccessAsync(CancellationToken ct = default)
    {
        var user = await GetCurrentUserAsync(ct);
        return user?.IsAdmin == true || user?.Role == UserRole.Editor;
    }
}
