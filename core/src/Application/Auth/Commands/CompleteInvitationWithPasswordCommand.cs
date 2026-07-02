using Mediator;
using Nona.Application.Admin.Common;
using Nona.Application.Auth.DTOs;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Interfaces;

namespace Nona.Application.Auth.Commands;

public record CompleteInvitationWithPasswordCommand(string Token, string NewPassword) : IRequest<LoginResult>;

public sealed class CompleteInvitationWithPasswordCommandHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService,
    IDateTime dateTime) : IRequestHandler<CompleteInvitationWithPasswordCommand, LoginResult>
{
    public async ValueTask<LoginResult> Handle(CompleteInvitationWithPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByInviteTokenHashAsync(TokenHelper.Hash(request.Token), cancellationToken);
        if (user is null)
        {
            return new LoginResult(false, null, "Invitation is invalid or has already been used.", AuthErrorCodes.InvitationInvalidOrUsed);
        }

        var (hash, salt) = passwordHasher.HashPassword(request.NewPassword);
        user.PasswordHash = hash;
        user.PasswordSalt = salt;
        user.InviteTokenHash = null;
        user.UpdatedAt = dateTime.NowUtc;

        await userRepository.UpdateAsync(user, cancellationToken);

        var token = jwtTokenService.GenerateToken(user);
        var expiresAt = DateTime.UtcNow.AddHours(24);

        return new LoginResult(
            true,
            new LoginResponse(
                token,
                user.Email,
                user.Role.ToString().ToLowerInvariant(),
                expiresAt),
            null);
    }
}
