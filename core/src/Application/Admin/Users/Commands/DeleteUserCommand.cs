using MediatR;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Entities;
using Nona.Domain.Interfaces;

namespace Nona.Application.Admin.Users.Commands;

public record DeleteUserCommand(long Id) : IRequest<DeleteUserResult>;

public record DeleteUserResult(bool Success, string? Error);

public class DeleteUserCommandHandler(
    IUserRepository userRepository,
    IProjectMemberRepository projectMemberRepository,
    IUserAuthorizationService userAuthorizationService,
    IAuditLogService? auditLogService = null) : IRequestHandler<DeleteUserCommand, DeleteUserResult>
{
    public async Task<DeleteUserResult> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.Id, cancellationToken);
        if (user is null)
            return new DeleteUserResult(false, "User not found");

        var currentUser = await userAuthorizationService.GetCurrentUserAsync(cancellationToken);

        if (string.Equals(user.Email, currentUser?.Email, StringComparison.OrdinalIgnoreCase))
            return new DeleteUserResult(false, "You cannot delete your own user account");

        var canManageUsers = currentUser?.IsAdmin == true || currentUser?.Role == UserRole.Editor;
        if (!canManageUsers || (user.IsAdmin && currentUser?.IsAdmin != true))
            return new DeleteUserResult(false, "Access denied");

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
