using MediatR;
using Nona.Application.Admin.Projects;
using Nona.Application.Admin.Users.DTOs;
using Nona.Application.Common;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Entities;
using Nona.Domain.Interfaces;

namespace Nona.Application.Admin.Users.Commands;

public record ProjectAccessRequest(string Role);
public record SetProjectAccessCommand(long UserId, string ProjectId, string Role) : IRequest<SetProjectAccessResult>;
public record SetProjectAccessResult(bool Success, ProjectAccessDto? ProjectAccess, string? Error);

public class SetProjectAccessCommandHandler(
    IUserRepository userRepository,
    IProjectRepository projectRepository,
    IProjectMemberRepository projectMemberRepository,
    IUserAuthorizationService userAuthorizationService) : IRequestHandler<SetProjectAccessCommand, SetProjectAccessResult>
{
    public async Task<SetProjectAccessResult> Handle(SetProjectAccessCommand request, CancellationToken cancellationToken)
    {
        var currentUser = await userAuthorizationService.GetCurrentUserAsync(cancellationToken);
        var canManageUsers = currentUser?.IsAdmin == true || currentUser?.Role == UserRole.Editor;
        if (!canManageUsers)
            return new SetProjectAccessResult(false, null, "Access denied");

        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
            return new SetProjectAccessResult(false, null, "User not found");

        if (user.IsAdmin && currentUser?.IsAdmin != true)
            return new SetProjectAccessResult(false, null, "Access denied");

        var project = await ProjectResolution.ResolveProjectAsync(
            projectRepository,
            request.ProjectId,
            cancellationToken);
        if (project is null)
            return new SetProjectAccessResult(false, null, "Project not found");

        var role = ParseProjectRole(request.Role);
        if (role is null)
            return new SetProjectAccessResult(false, null, "Invalid role. Must be 'viewer' or 'editor'");

        var projectName = project.Name;
        var existingMember = await projectMemberRepository.GetAsync(user.Email, projectName, cancellationToken);

        if (existingMember is not null)
        {
            await projectMemberRepository.DeleteAsync(user.Email, projectName, cancellationToken);
        }

        var member = new ProjectMember
        {
            Username = user.Email,
            ProjectId = projectName,
            Role = role.Value,
            CreatedAt = DateTime.UtcNow
        };

        await projectMemberRepository.AddAsync(member, cancellationToken);

        var dto = new ProjectAccessDto(member.ProjectId, member.Role.ToApiString());
        return new SetProjectAccessResult(true, dto, null);
    }

    private static ProjectRole? ParseProjectRole(string? role)
    {
        if (role is null)
            return null;

        return role.ToLowerInvariant() switch
        {
            "viewer" => ProjectRole.Viewer,
            "editor" => ProjectRole.Editor,
            _ => null
        };
    }

}
