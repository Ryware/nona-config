using MediatR;
using Nona.Domain.Interfaces;

namespace Nona.Application.Admin.Users.Commands;

public record RemoveProjectAccessCommand(long UserId, string ProjectId) : IRequest<RemoveProjectAccessResult>;
public record RemoveProjectAccessResult(bool Success, string? Error);

public class RemoveProjectAccessCommandHandler(
    IUserRepository userRepository,
    IProjectMemberRepository projectMemberRepository) : IRequestHandler<RemoveProjectAccessCommand, RemoveProjectAccessResult>
{
    public async Task<RemoveProjectAccessResult> Handle(RemoveProjectAccessCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
            return new RemoveProjectAccessResult(false, "User not found");

        if (!await projectMemberRepository.ExistsAsync(user.Email, request.ProjectId, cancellationToken))
            return new RemoveProjectAccessResult(false, "Project access not found");

        await projectMemberRepository.DeleteAsync(user.Email, request.ProjectId, cancellationToken);

        return new RemoveProjectAccessResult(true, null);
    }
}
