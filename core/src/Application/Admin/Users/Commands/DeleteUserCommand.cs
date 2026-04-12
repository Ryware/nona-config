using MediatR;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Interfaces;

namespace Nona.Application.Admin.Users.Commands;

public record DeleteUserCommand(long Id) : IRequest<DeleteUserResult>;

public record DeleteUserResult(bool Success, string? Error);

public class DeleteUserCommandHandler(
    IUserRepository userRepository,
    IProjectMemberRepository projectMemberRepository,
    IAuditLogService? auditLogService = null) : IRequestHandler<DeleteUserCommand, DeleteUserResult>
{
    public async Task<DeleteUserResult> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.Id, cancellationToken);
        if (user is null)
            return new DeleteUserResult(false, "User not found");

        await projectMemberRepository.DeleteByUserAsync(user.Email, cancellationToken);
        await userRepository.DeleteAsync(user.Email, cancellationToken);

        if (auditLogService is not null)
        {
            await auditLogService.WriteAsync(
                "Deleted User",
                user.Email,
                cancellationToken: cancellationToken);
        }

        return new DeleteUserResult(true, null);
    }
}
