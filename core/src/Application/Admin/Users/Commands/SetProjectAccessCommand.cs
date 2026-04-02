using MediatR;
using Nona.Application.Admin.Users.DTOs;
using Nona.Application.Common;
using Nona.Domain.Entities;
using Nona.Domain.Interfaces;

namespace Nona.Application.Admin.Users.Commands;

public record ProjectAccessRequest(string Role);
public record SetProjectAccessCommand(long UserId, string ProjectId, string Role) : IRequest<SetProjectAccessResult>;
public record SetProjectAccessResult(bool Success, ProjectAccessDto? ProjectAccess, string? Error);

public class SetProjectAccessCommandHandler(
    IUserRepository userRepository,
    IProjectRepository projectRepository,
    IProjectMemberRepository projectMemberRepository) : IRequestHandler<SetProjectAccessCommand, SetProjectAccessResult>
{
    public async Task<SetProjectAccessResult> Handle(SetProjectAccessCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
            return new SetProjectAccessResult(false, null, "User not found");

        if (!await projectRepository.ExistsAsync(request.ProjectId, cancellationToken))
            return new SetProjectAccessResult(false, null, "Project not found");

        var role = ParseProjectRole(request.Role);
        if (role is null)
            return new SetProjectAccessResult(false, null, "Invalid role. Must be 'viewer' or 'editor'");

        var existingMember = await projectMemberRepository.GetAsync(user.Email, request.ProjectId, cancellationToken);

        if (existingMember is not null)
        {
            await projectMemberRepository.DeleteAsync(user.Email, request.ProjectId, cancellationToken);
        }

        var member = new ProjectMember
        {
            Username = user.Email,
            ProjectId = request.ProjectId,
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
