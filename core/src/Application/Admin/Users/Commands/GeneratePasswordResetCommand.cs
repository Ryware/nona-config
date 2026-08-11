using Mediator;
using Nona.Application.Admin.Common;
using Nona.Application.Admin.Users.DTOs;
using Nona.Application.Auth;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Entities;
using Nona.Domain.Enums;
using Nona.Domain.Interfaces;

namespace Nona.Application.Admin.Users.Commands;

public record GeneratePasswordResetCommand(long UserId) : IRequest<GeneratePasswordResetResult>;

public record GeneratePasswordResetResult(
    bool Success,
    GeneratePasswordResetResponse? Response,
    string? Error,
    string? ErrorCode = null);

public sealed class GeneratePasswordResetCommandHandler(
    IUserRepository userRepository,
    IUserAuthorizationService userAuthorizationService,
    IDateTime dateTime,
    IAuditLogService? auditLogService = null)
    : IRequestHandler<GeneratePasswordResetCommand, GeneratePasswordResetResult>
{
    internal static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(24);

    public async ValueTask<GeneratePasswordResetResult> Handle(
        GeneratePasswordResetCommand request,
        CancellationToken cancellationToken)
    {
        var currentUser = await userAuthorizationService.GetCurrentUserAsync(cancellationToken);
        if (currentUser?.Role != UserRole.Admin)
        {
            return new GeneratePasswordResetResult(false, null, "Access denied");
        }

        if (currentUser.Id == request.UserId)
        {
            return new GeneratePasswordResetResult(
                false,
                null,
                "You cannot generate a password reset link for your own account.",
                AuthErrorCodes.PasswordResetSelfNotAllowed);
        }

        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            return new GeneratePasswordResetResult(false, null, "User not found");
        }

        if (string.IsNullOrEmpty(user.PasswordHash))
        {
            return new GeneratePasswordResetResult(
                false,
                null,
                "Password reset is not available for this account.",
                AuthErrorCodes.PasswordResetUnavailable);
        }

        var now = dateTime.NowUtc;
        var token = TokenHelper.Generate();
        var expiresAt = now.Add(TokenLifetime);
        user.PasswordResetTokenHash = TokenHelper.Hash(token);
        user.PasswordResetTokenExpiresAt = expiresAt;
        user.UpdatedAt = now;

        await userRepository.UpdateAsync(user, cancellationToken);

        if (auditLogService is not null)
        {
            await auditLogService.WriteAsync(
                AuditActionKind.Activity,
                "Generated Password Reset",
                user.Email,
                cancellationToken: cancellationToken);
        }

        return new GeneratePasswordResetResult(
            true,
            new GeneratePasswordResetResponse(token, expiresAt),
            null);
    }
}
