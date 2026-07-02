using Mediator;
using Nona.Application.Admin.Users.DTOs;
using Nona.Application.Common;
using Nona.Domain.Interfaces;

namespace Nona.Application.Admin.Users.Queries;

public record ListUsersQuery : IRequest<IReadOnlyList<UserDto>>;

public class ListUsersQueryHandler(IUserRepository userRepository, IProjectMemberRepository projectMemberRepository) : IRequestHandler<ListUsersQuery, IReadOnlyList<UserDto>>
{
    public async ValueTask<IReadOnlyList<UserDto>> Handle(ListUsersQuery request, CancellationToken cancellationToken)
    {
        var users = await userRepository.ListAsync(cancellationToken);
        var result = new List<UserDto>();

        foreach (var user in users)
        {
            var members = await projectMemberRepository.ListByUserAsync(user.Email, cancellationToken);
            var projects = members.Select(m => new ProjectAccessDto(m.ProjectId, m.Role.ToApiString())).ToList();

            result.Add(new UserDto(
                user.Id,
                user.Email,
                user.Name,
                user.Role.ToApiString(),
                user.Scope.ToApiString(),
                user.IsAdmin,
                projects,
                user.CreatedAt,
                user.UpdatedAt));
        }

        return result;
    }
}
