using Nona.Application.Admin.Users.DTOs;
using Nona.Application.Common;
using Nona.Domain.Entities;

namespace Nona.Application.Admin.Users;

internal static class UserDtoMapping
{
    public static UserDto ToDto(User user, IEnumerable<ProjectMember> memberships)
    {
        return new UserDto(
            user.Id,
            user.Email,
            user.Name,
            user.Role.ToApiString(),
            user.Scope.ToApiString(),
            ToProjectAccessDtos(memberships),
            !string.IsNullOrEmpty(user.PasswordHash),
            user.CreatedAt,
            user.UpdatedAt);
    }

    public static IReadOnlyList<ProjectAccessDto> ToProjectAccessDtos(IEnumerable<ProjectMember> memberships)
        => memberships.Select(ToProjectAccessDto).ToList();

    public static ProjectAccessDto ToProjectAccessDto(ProjectMember membership)
        => new(membership.ProjectId, membership.Role.ToApiString());
}
