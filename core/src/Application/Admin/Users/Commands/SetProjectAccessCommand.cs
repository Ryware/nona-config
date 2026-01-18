using MediatR;
using Nona.Application.Admin.Users.DTOs;
using Nona.Application.Common;
using Nona.Domain.Entities;
using Nona.Domain.Interfaces;

namespace Nona.Application.Admin.Users.Commands;

public record ProjectAccessRequest(string Role);
public record SetProjectAccessCommand(string Username, string ProjectName, string Role) : IRequest<SetProjectAccessResult>;
public record SetProjectAccessResult(bool Success, ProjectAccessDto? ProjectAccess, string? Error);

public class SetProjectAccessCommandHandler(
    IUserRepository userRepository,
    IProjectRepository projectRepository,
    IProjectMemberRepository projectMemberRepository) : IRequestHandler<SetProjectAccessCommand, SetProjectAccessResult>
{
    public async Task<SetProjectAccessResult> Handle(SetProjectAccessCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetAsync(request.Username, cancellationToken);
        if (user is null)
            return new SetProjectAccessResult(false, null, "User not found");

        if (user.Role == UserRole.Admin)
            return new SetProjectAccessResult(false, null, "Cannot assign project access to admin users. Admin users have full access to all projects.");

        if (!await projectRepository.ExistsAsync(request.ProjectName, cancellationToken))
            return new SetProjectAccessResult(false, null, "Project not found");

        var role = ParseProjectRole(request.Role);
        if (role is null)
            return new SetProjectAccessResult(false, null, "Invalid role. Must be 'viewer' or 'admin'");

        var existingMember = await projectMemberRepository.GetAsync(request.Username, request.ProjectName, cancellationToken);

        if (existingMember is not null)
        {
            // Update existing - need to create new since ProjectMember has init-only Role
            await projectMemberRepository.DeleteAsync(request.Username, request.ProjectName, cancellationToken);
        }

        var member = new ProjectMember
        {
            Username = request.Username,
            ProjectName = request.ProjectName,
            Role = role.Value,
            CreatedAt = DateTime.UtcNow
        };

        await projectMemberRepository.AddAsync(member, cancellationToken);

        var dto = new ProjectAccessDto(member.ProjectName, member.Role.ToApiString());
        return new SetProjectAccessResult(true, dto, null);
    }

    private static ProjectRole? ParseProjectRole(string? role)
    {
        if (role is null)
            return null;

        return role.ToLowerInvariant() switch
        {
            "viewer" => ProjectRole.User,
            "admin" => ProjectRole.Admin,
            _ => null
        };
    }
}
