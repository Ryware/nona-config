using MediatR;
using Nona.Application.Admin.Users.DTOs;
using Nona.Application.Common;
using Nona.Domain.Interfaces;

namespace Nona.Application.Admin.Users.Queries;

public record GetUserQuery(long Id) : IRequest<GetUserResult>;

public record GetUserResult(bool Success, UserDto? User, string? Error);

public class GetUserQueryHandler(IUserRepository userRepository, IProjectMemberRepository projectMemberRepository) : IRequestHandler<GetUserQuery, GetUserResult>
{
    public async Task<GetUserResult> Handle(GetUserQuery request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.Id, cancellationToken);

        if (user is null)
            return new GetUserResult(false, null, "User not found");

        var members = await projectMemberRepository.ListByUserAsync(user.Email, cancellationToken);
        var projects = members.Select(m => new ProjectAccessDto(m.ProjectId, m.Role.ToApiString())).ToList();

        var dto = new UserDto(
            user.Id,
            user.Email,
            user.Name,
            user.Role.ToApiString(),
            user.Scope.ToApiString(),
            user.IsAdmin,
            projects,
            user.CreatedAt,
            user.UpdatedAt);

        return new GetUserResult(true, dto, null);
    }
}
