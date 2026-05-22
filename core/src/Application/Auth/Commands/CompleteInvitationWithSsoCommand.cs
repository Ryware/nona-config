using MediatR;
using Microsoft.Extensions.Logging;
using Nona.Application.Admin.Common;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Interfaces;

namespace Nona.Application.Auth.Commands;

public record CompleteInvitationWithSsoCommand(string Token, string Provider, string IdToken) : IRequest<LoginResult>;

public sealed class CompleteInvitationWithSsoCommandHandler(
    IEnumerable<ISsoTokenValidator> tokenValidators,
    IExternalIdentityRepository externalIdentityRepository,
    IUserRepository userRepository,
    IJwtTokenService jwtTokenService,
    IDateTime dateTime,
    ILogger<CompleteInvitationWithSsoCommandHandler> logger) : IRequestHandler<CompleteInvitationWithSsoCommand, LoginResult>
{
    public async Task<LoginResult> Handle(CompleteInvitationWithSsoCommand request, CancellationToken cancellationToken)
    {
        var invitedUser = await userRepository.GetByInviteTokenHashAsync(TokenHelper.Hash(request.Token), cancellationToken);
        if (invitedUser is null)
        {
            return new LoginResult(false, null, "Invitation is invalid or has already been used.", AuthErrorCodes.InvitationInvalidOrUsed);
        }

        return await SsoLoginWorkflow.AuthenticateAsync(
            request.Provider,
            request.IdToken,
            tokenValidators,
            externalIdentityRepository,
            userRepository,
            jwtTokenService,
            dateTime,
            logger,
            invitedUser.Email,
            cancellationToken);
    }
}
