using Mediator;
using Nona.Application.Admin.Users;
using Nona.Application.Admin.Users.DTOs;
using Nona.Domain.Interfaces;

namespace Nona.Application.Admin.Users.Queries;

public record GetUserQuery(long Id) : IRequest<GetUserResult>;

public record GetUserResult(bool Success, UserDto? User, string? Error);

public class GetUserQueryHandler(IUserRepository userRepository, IProjectMemberRepository projectMemberRepository) : IRequestHandler<GetUserQuery, GetUserResult>
{
    public async ValueTask<GetUserResult> Handle(GetUserQuery request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.Id, cancellationToken);

        if (user is null)
            return new GetUserResult(false, null, "User not found");

        var members = await projectMemberRepository.ListByUserAsync(user.Email, cancellationToken);
        var dto = UserDtoMapping.ToDto(user, members);

        return new GetUserResult(true, dto, null);
    }
}
