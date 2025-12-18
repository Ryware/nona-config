using Nona.Domain.Entities;
using Nona.Domain.Interfaces;

namespace Nona.Application.Common.Interfaces;

public interface IProjectAccessService
{
    Task<bool> HasAccessAsync(string projectName, CancellationToken ct = default);
    Task<bool> HasAdminAccessAsync(string projectName, CancellationToken ct = default);
}

public class ProjectAccessService(
    ICurrentUserService currentUserService,
    IProjectMemberRepository projectMemberRepository) : IProjectAccessService
{
    public async Task<bool> HasAccessAsync(string projectName, CancellationToken ct = default)
    {
        // Admin users have access to all projects
        if (currentUserService.IsAdmin)
            return true;

        var username = currentUserService.Username;
        if (string.IsNullOrEmpty(username))
            return false;

        return await projectMemberRepository.ExistsAsync(username, projectName, ct);
    }

    public async Task<bool> HasAdminAccessAsync(string projectName, CancellationToken ct = default)
    {
        // Admin users have admin access to all projects
        if (currentUserService.IsAdmin)
            return true;

        var username = currentUserService.Username;
        if (string.IsNullOrEmpty(username))
            return false;

        var member = await projectMemberRepository.GetAsync(username, projectName, ct);
        return member?.Role == ProjectRole.Admin;
    }
}
