using Mediator;
using Nona.Application.Admin.Common;
using Nona.Application.Auth.DTOs;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Interfaces;

namespace Nona.Application.Auth.Queries;

public record GetPasswordResetQuery(string Token) : IRequest<GetPasswordResetResult>;

public record GetPasswordResetResult(
    bool Success,
    PasswordResetDetailsResponse? PasswordReset,
    string? Error,
    string? ErrorCode = null);

public sealed class GetPasswordResetQueryHandler(
    IUserRepository userRepository,
    IDateTime dateTime) : IRequestHandler<GetPasswordResetQuery, GetPasswordResetResult>
{
    public async ValueTask<GetPasswordResetResult> Handle(
        GetPasswordResetQuery request,
        CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByPasswordResetTokenHashAsync(
            TokenHelper.Hash(request.Token),
            cancellationToken);
        if (user?.PasswordResetTokenExpiresAt is not { } expiresAt || expiresAt <= dateTime.NowUtc)
        {
            return Invalid();
        }

        return new GetPasswordResetResult(
            true,
            new PasswordResetDetailsResponse(user.Email, user.Name, expiresAt),
            null);
    }

    private static GetPasswordResetResult Invalid() => new(
        false,
        null,
        "Password reset link is invalid, expired, or has already been used.",
        AuthErrorCodes.PasswordResetInvalidOrUsed);
}
