using Mediator;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Enums;
using Nona.Domain.Interfaces;

namespace Nona.Application.Auth.Commands;

public record ChangePasswordCommand(string CurrentPassword, string NewPassword)
    : IRequest<ChangePasswordResult>;

public record ChangePasswordResult(bool Success, string? Error, string? ErrorCode = null);

public sealed class ChangePasswordCommandHandler(
    IUserAuthorizationService userAuthorizationService,
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IDateTime dateTime,
    IAuditLogService? auditLogService = null)
    : IRequestHandler<ChangePasswordCommand, ChangePasswordResult>
{
    public async ValueTask<ChangePasswordResult> Handle(
        ChangePasswordCommand request,
        CancellationToken cancellationToken)
    {
        var user = await userAuthorizationService.GetCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return new ChangePasswordResult(false, "User not found");
        }

        if (string.IsNullOrEmpty(user.PasswordHash))
        {
            return new ChangePasswordResult(
                false,
                "Password change is not available for this account.",
                AuthErrorCodes.PasswordChangeUnavailable);
        }

        if (!passwordHasher.VerifyPassword(request.CurrentPassword, user.PasswordHash))
        {
            return new ChangePasswordResult(
                false,
                "Current password is incorrect.",
                AuthErrorCodes.CurrentPasswordInvalid);
        }

        if (passwordHasher.VerifyPassword(request.NewPassword, user.PasswordHash))
        {
            return new ChangePasswordResult(
                false,
                "New password must be different from the current password.",
                AuthErrorCodes.NewPasswordMustDiffer);
        }

        var (passwordHash, passwordSalt) = passwordHasher.HashPassword(request.NewPassword);
        user.PasswordHash = passwordHash;
        user.PasswordSalt = passwordSalt;
        user.PasswordResetTokenHash = null;
        user.PasswordResetTokenExpiresAt = null;
        user.UpdatedAt = dateTime.NowUtc;
        await userRepository.UpdateAsync(user, cancellationToken);

        if (auditLogService is not null)
        {
            await auditLogService.WriteAsync(
                AuditActionKind.Activity,
                "Changed Password",
                user.Email,
                cancellationToken: cancellationToken);
        }

        return new ChangePasswordResult(true, null);
    }
}
