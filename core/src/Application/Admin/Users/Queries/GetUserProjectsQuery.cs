using Mediator;
using Nona.Application.Admin.Users;
using Nona.Application.Admin.Users.DTOs;
using Nona.Domain.Interfaces;

namespace Nona.Application.Admin.Users.Queries;

public record GetUserProjectsQuery(long Id) : IRequest<GetUserProjectsResult>;
public record GetUserProjectsResult(bool Success, IReadOnlyList<ProjectAccessDto>? Projects, string? Error);

public class GetUserProjectsQueryHandler(
    IUserRepository userRepository,
    IProjectMemberRepository projectMemberRepository) : IRequestHandler<GetUserProjectsQuery, GetUserProjectsResult>
{
    public async ValueTask<GetUserProjectsResult> Handle(GetUserProjectsQuery request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.Id, cancellationToken);
        if (user is null)
            return new GetUserProjectsResult(false, null, "User not found");

        var members = await projectMemberRepository.ListByUserAsync(user.Email, cancellationToken);
        var projects = UserDtoMapping.ToProjectAccessDtos(members);

        return new GetUserProjectsResult(true, projects, null);
    }
}
