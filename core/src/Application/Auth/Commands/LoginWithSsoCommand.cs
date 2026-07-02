using Mediator;
using Microsoft.Extensions.Logging;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Interfaces;

namespace Nona.Application.Auth.Commands;

public record LoginWithSsoCommand(string Provider, string IdToken) : IRequest<LoginResult>;

public sealed class LoginWithSsoCommandHandler(
    IEnumerable<ISsoTokenValidator> tokenValidators,
    IExternalIdentityRepository externalIdentityRepository,
    IUserRepository userRepository,
    IJwtTokenService jwtTokenService,
    IDateTime dateTime,
    ILogger<LoginWithSsoCommandHandler> logger) : IRequestHandler<LoginWithSsoCommand, LoginResult>
{
    public async ValueTask<LoginResult> Handle(LoginWithSsoCommand request, CancellationToken cancellationToken)
    {
        return await SsoLoginWorkflow.AuthenticateAsync(
            request.Provider,
            request.IdToken,
            tokenValidators,
            externalIdentityRepository,
            userRepository,
            jwtTokenService,
            dateTime,
            logger,
            cancellationToken: cancellationToken);
    }
}
