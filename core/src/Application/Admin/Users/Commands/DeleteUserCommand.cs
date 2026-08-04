using Mediator;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Entities;
using Nona.Domain.Enums;
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
    public async ValueTask<DeleteUserResult> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.Id, cancellationToken);
        if (user is null)
            return new DeleteUserResult(false, "User not found");

        var currentUser = await userAuthorizationService.GetCurrentUserAsync(cancellationToken);

        if (string.Equals(user.Email, currentUser?.Email, StringComparison.OrdinalIgnoreCase))
            return new DeleteUserResult(false, "You cannot delete your own user account");

        if (user.Role == UserRole.Admin)
            return new DeleteUserResult(false, "Admin user cannot be deleted");

        var canManageUsers = currentUser?.Role is UserRole.Admin or UserRole.Editor;
        if (!canManageUsers)
            return new DeleteUserResult(false, "Access denied");

        await projectMemberRepository.DeleteByUserAsync(user.Email, cancellationToken);
        await userRepository.DeleteAsync(user.Email, cancellationToken);

        if (auditLogService is not null)
        {
            await auditLogService.WriteAsync(
                AuditActionKind.Delete,
                "Deleted User",
                user.Email,
                cancellationToken: cancellationToken);
        }

        return new DeleteUserResult(true, null);
    }

}
