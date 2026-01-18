using MediatR;
using Nona.Domain.Interfaces;

namespace Nona.Application.Admin.Users.Commands;

public record RemoveProjectAccessCommand(string Username, string ProjectName) : IRequest<RemoveProjectAccessResult>;
public record RemoveProjectAccessResult(bool Success, string? Error);

public class RemoveProjectAccessCommandHandler(
    IUserRepository userRepository,
    IProjectMemberRepository projectMemberRepository) : IRequestHandler<RemoveProjectAccessCommand, RemoveProjectAccessResult>
{
    public async Task<RemoveProjectAccessResult> Handle(RemoveProjectAccessCommand request, CancellationToken cancellationToken)
    {
        if (!await userRepository.ExistsAsync(request.Username, cancellationToken))
            return new RemoveProjectAccessResult(false, "User not found");

        if (!await projectMemberRepository.ExistsAsync(request.Username, request.ProjectName, cancellationToken))
            return new RemoveProjectAccessResult(false, "Project access not found");

        await projectMemberRepository.DeleteAsync(request.Username, request.ProjectName, cancellationToken);

        return new RemoveProjectAccessResult(true, null);
    }
}
