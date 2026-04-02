using MediatR;
using Nona.Domain.Interfaces;

namespace Nona.Application.Admin.Users.Commands;

public record DeleteUserCommand(long Id) : IRequest<DeleteUserResult>;

public record DeleteUserResult(bool Success, string? Error);

public class DeleteUserCommandHandler(IUserRepository userRepository, IProjectMemberRepository projectMemberRepository) : IRequestHandler<DeleteUserCommand, DeleteUserResult>
{
    public async Task<DeleteUserResult> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.Id, cancellationToken);
        if (user is null)
            return new DeleteUserResult(false, "User not found");

        await projectMemberRepository.DeleteByUserAsync(user.Email, cancellationToken);
        await userRepository.DeleteAsync(user.Email, cancellationToken);

        return new DeleteUserResult(true, null);
    }
}
