using Mediator;
using Nona.Application.Admin.Common;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Enums;
using Nona.Domain.Interfaces;

namespace Nona.Application.Auth.Commands;

public record CompletePasswordResetCommand(string Token, string NewPassword)
    : IRequest<CompletePasswordResetResult>;

public record CompletePasswordResetResult(bool Success, string? Error, string? ErrorCode = null);

public sealed class CompletePasswordResetCommandHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IDateTime dateTime,
    IAuditLogService? auditLogService = null)
    : IRequestHandler<CompletePasswordResetCommand, CompletePasswordResetResult>
{
    public async ValueTask<CompletePasswordResetResult> Handle(
        CompletePasswordResetCommand request,
        CancellationToken cancellationToken)
    {
        var tokenHash = TokenHelper.Hash(request.Token);
        var user = await userRepository.GetByPasswordResetTokenHashAsync(tokenHash, cancellationToken);
        var now = dateTime.NowUtc;
        if (user?.PasswordResetTokenExpiresAt is null || user.PasswordResetTokenExpiresAt <= now)
        {
            return Invalid();
        }

        var (passwordHash, passwordSalt) = passwordHasher.HashPassword(request.NewPassword);
        var consumed = await userRepository.TryResetPasswordAsync(
            tokenHash,
            now,
            passwordHash,
            passwordSalt,
            now,
            cancellationToken);
        if (!consumed)
        {
            return Invalid();
        }

        if (auditLogService is not null)
        {
            await auditLogService.WriteAsAsync(
                "Password Reset Link",
                true,
                AuditActionKind.Activity,
                "Reset Password",
                user.Email,
                cancellationToken: cancellationToken);
        }

        return new CompletePasswordResetResult(true, null);
    }

    private static CompletePasswordResetResult Invalid() => new(
        false,
        "Password reset link is invalid, expired, or has already been used.",
        AuthErrorCodes.PasswordResetInvalidOrUsed);
}
