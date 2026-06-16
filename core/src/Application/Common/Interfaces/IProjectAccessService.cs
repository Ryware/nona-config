using Nona.Domain.Entities;
using Nona.Domain.Interfaces;

namespace Nona.Application.Common.Interfaces;

public interface IProjectAccessService
{
    Task<bool> HasViewAccessAsync(string projectName, CancellationToken ct = default);
    Task<bool> HasEditAccessAsync(string projectName, CancellationToken ct = default);
}

public class ProjectAccessService(
    ICurrentUserService currentUserService,
    IUserAuthorizationService userAuthorizationService,
    IProjectMemberRepository projectMemberRepository) : IProjectAccessService
{
    public async Task<bool> HasViewAccessAsync(string projectName, CancellationToken ct = default)
    {
        if (await userAuthorizationService.HasGlobalProjectAccessAsync(ct))
            return true;

        var currentUser = await userAuthorizationService.GetCurrentUserAsync(ct);
        var username = currentUser?.Email ?? currentUserService.Username;
        if (string.IsNullOrWhiteSpace(username))
            return false;

        return await projectMemberRepository.ExistsAsync(username, projectName, ct);
    }

    public async Task<bool> HasEditAccessAsync(string projectName, CancellationToken ct = default)
    {
        if (await userAuthorizationService.HasGlobalProjectAccessAsync(ct))
            return true;

        var currentUser = await userAuthorizationService.GetCurrentUserAsync(ct);
        var username = currentUser?.Email ?? currentUserService.Username;
        if (string.IsNullOrWhiteSpace(username))
            return false;

        var member = await projectMemberRepository.GetAsync(username, projectName, ct);
        return member?.Role == ProjectRole.Editor;
    }
}
