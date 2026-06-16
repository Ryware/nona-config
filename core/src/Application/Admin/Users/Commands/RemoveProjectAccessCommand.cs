using MediatR;
using Nona.Application.Admin.Projects;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Entities;
using Nona.Domain.Interfaces;

namespace Nona.Application.Admin.Users.Commands;

public record RemoveProjectAccessCommand(long UserId, string ProjectId) : IRequest<RemoveProjectAccessResult>;
public record RemoveProjectAccessResult(bool Success, string? Error);

public class RemoveProjectAccessCommandHandler(
    IUserRepository userRepository,
    IProjectRepository projectRepository,
    IProjectMemberRepository projectMemberRepository,
    IUserAuthorizationService userAuthorizationService) : IRequestHandler<RemoveProjectAccessCommand, RemoveProjectAccessResult>
{
    public async Task<RemoveProjectAccessResult> Handle(RemoveProjectAccessCommand request, CancellationToken cancellationToken)
    {
        var currentUser = await userAuthorizationService.GetCurrentUserAsync(cancellationToken);
        var canManageUsers = currentUser?.IsAdmin == true || currentUser?.Role == UserRole.Editor;
        if (!canManageUsers)
            return new RemoveProjectAccessResult(false, "Access denied");

        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
            return new RemoveProjectAccessResult(false, "User not found");

        if (user.IsAdmin && currentUser?.IsAdmin != true)
            return new RemoveProjectAccessResult(false, "Access denied");

        var project = await ProjectResolution.ResolveProjectAsync(
            projectRepository,
            request.ProjectId,
            cancellationToken);
        var projectName = project?.Name ?? request.ProjectId;

        if (!await projectMemberRepository.ExistsAsync(user.Email, projectName, cancellationToken))
            return new RemoveProjectAccessResult(false, "Project access not found");

        await projectMemberRepository.DeleteAsync(user.Email, projectName, cancellationToken);

        return new RemoveProjectAccessResult(true, null);
    }
}
