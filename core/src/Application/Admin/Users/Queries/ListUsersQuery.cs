using Mediator;
using Nona.Application.Admin.Users;
using Nona.Application.Admin.Users.DTOs;
using Nona.Domain.Interfaces;

namespace Nona.Application.Admin.Users.Queries;

public record ListUsersQuery : IRequest<IReadOnlyList<UserDto>>;

public class ListUsersQueryHandler(IUserRepository userRepository, IProjectMemberRepository projectMemberRepository) : IRequestHandler<ListUsersQuery, IReadOnlyList<UserDto>>
{
    public async ValueTask<IReadOnlyList<UserDto>> Handle(ListUsersQuery request, CancellationToken cancellationToken)
    {
        var users = await userRepository.ListAsync(cancellationToken);
        if (users.Count == 0)
            return [];

        var members = await projectMemberRepository.ListByUsersAsync(
            users.Select(user => user.Email).ToArray(),
            cancellationToken);
        var membersByUser = members.ToLookup(member => member.Username, StringComparer.OrdinalIgnoreCase);

        return users
            .Select(user => UserDtoMapping.ToDto(user, membersByUser[user.Email]))
            .ToList();
    }
}
