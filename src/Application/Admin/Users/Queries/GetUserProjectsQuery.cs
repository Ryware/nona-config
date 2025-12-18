using MediatR;
using Nona.Application.Admin.Users.DTOs;
using Nona.Application.Common;
using Nona.Domain.Interfaces;

namespace Nona.Application.Admin.Users.Queries;

public record GetUserProjectsQuery(string Username) : IRequest<GetUserProjectsResult>;
public record GetUserProjectsResult(bool Success, IReadOnlyList<ProjectAccessDto>? Projects, string? Error);

public class GetUserProjectsQueryHandler(
    IUserRepository userRepository,
    IProjectMemberRepository projectMemberRepository) : IRequestHandler<GetUserProjectsQuery, GetUserProjectsResult>
{
    public async Task<GetUserProjectsResult> Handle(GetUserProjectsQuery request, CancellationToken cancellationToken)
    {
        if (!await userRepository.ExistsAsync(request.Username, cancellationToken))
            return new GetUserProjectsResult(false, null, "User not found");

        var members = await projectMemberRepository.ListByUserAsync(request.Username, cancellationToken);

        var projects = members
            .Select(m => new ProjectAccessDto(m.ProjectName, m.Role.ToApiString()))
            .ToList();

        return new GetUserProjectsResult(true, projects, null);
    }
}
