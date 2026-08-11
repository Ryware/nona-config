using Mediator;
using Nona.Application.Common;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Entities;
using Nona.Domain.Enums;
using Nona.Domain.Interfaces;

namespace Nona.Application.Admin.Users.Commands;

public record DeleteUserCommand(long Id) : IRequest<DeleteUserResult>;

public record DeleteUserResult(bool Success, string? Error, string? ErrorCode = null);

public class DeleteUserCommandHandler(
    IUserRepository userRepository,
    IProjectMemberRepository projectMemberRepository,
    IUserAuthorizationService userAuthorizationService,
    IAuditLogService? auditLogService = null) : IRequestHandler<DeleteUserCommand, DeleteUserResult>
{
    public async ValueTask<DeleteUserResult> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        if (!await userAuthorizationService.CanManageUsersAsync(cancellationToken))
            return new DeleteUserResult(false, "Access denied", AuthorizationErrorCodes.AccessDenied);

        var user = await userRepository.GetByIdAsync(request.Id, cancellationToken);
        if (user is null)
            return new DeleteUserResult(false, "User not found");

        var currentUser = await userAuthorizationService.GetCurrentUserAsync(cancellationToken);

        if (string.Equals(user.Email, currentUser?.Email, StringComparison.OrdinalIgnoreCase))
            return new DeleteUserResult(false, "You cannot delete your own user account");

        if (user.Role == UserRole.Admin)
        {
            var users = await userRepository.ListAsync(cancellationToken);
            if (users.Count(candidate => candidate.Role == UserRole.Admin) <= 1)
                return new DeleteUserResult(false, "At least one admin is required");
        }

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
