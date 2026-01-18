using MediatR;
using Nona.Domain.Interfaces;

namespace Nona.Application.Admin.Users.Commands;

public record DeleteUserCommand(string Username) : IRequest<DeleteUserResult>;

public record DeleteUserResult(bool Success, string? Error);

public class DeleteUserCommandHandler(IUserRepository userRepository, IProjectMemberRepository projectMemberRepository) : IRequestHandler<DeleteUserCommand, DeleteUserResult>
{
    public async Task<DeleteUserResult> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        if (!await userRepository.ExistsAsync(request.Username, cancellationToken))
            return new DeleteUserResult(false, "User not found");

        await projectMemberRepository.DeleteByUserAsync(request.Username, cancellationToken);
        await userRepository.DeleteAsync(request.Username, cancellationToken);

        return new DeleteUserResult(true, null);
    }
}
