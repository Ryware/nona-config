using MediatR;
using Microsoft.Extensions.Logging;
using Nona.Application.Auth.DTOs;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Entities;
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
    private const string GenericError = "Authentication failed";
    private const string UserNotRegisteredErrorCode = "sso_user_not_registered";

    public async Task<LoginResult> Handle(LoginWithSsoCommand request, CancellationToken cancellationToken)
    {
        var validator = tokenValidators.FirstOrDefault(candidate =>
            string.Equals(candidate.Provider, request.Provider, StringComparison.OrdinalIgnoreCase));

        if (validator is null || !validator.IsEnabled)
        {
            logger.LogWarning(
                "SSO authentication rejected. Provider={Provider} FailureCategory={FailureCategory}",
                request.Provider,
                "provider_disabled");

            return new LoginResult(false, null, GenericError);
        }

        var validation = await validator.ValidateAsync(request.IdToken, cancellationToken);
        if (!validation.Success || validation.Identity is null)
        {
            logger.LogWarning(
                "SSO authentication rejected. Provider={Provider} FailureCategory={FailureCategory}",
                request.Provider,
                validation.FailureCategory);

            return new LoginResult(false, null, GenericError);
        }

        var identity = validation.Identity;
        var linkedIdentity = await externalIdentityRepository.GetAsync(
            identity.Provider,
            identity.Issuer,
            identity.Subject,
            cancellationToken);

        User? user;
        if (linkedIdentity is not null)
        {
            user = await userRepository.GetAsync(linkedIdentity.UserEmail, cancellationToken);
            if (user is null)
            {
                logger.LogWarning(
                    "SSO authentication rejected. Provider={Provider} FailureCategory={FailureCategory} Issuer={Issuer} Email={Email}",
                    identity.Provider,
                    "linked_user_missing",
                    identity.Issuer,
                    linkedIdentity.UserEmail);

                return new LoginResult(false, null, GenericError, UserNotRegisteredErrorCode);
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(identity.Email))
            {
                logger.LogWarning(
                    "SSO authentication rejected. Provider={Provider} FailureCategory={FailureCategory} Issuer={Issuer}",
                    identity.Provider,
                    "email_missing",
                    identity.Issuer);

                return new LoginResult(false, null, GenericError);
            }

            user = await userRepository.GetAsync(identity.Email, cancellationToken);
            if (user is null)
            {
                logger.LogWarning(
                    "SSO authentication rejected. Provider={Provider} FailureCategory={FailureCategory} Issuer={Issuer} Email={Email}",
                    identity.Provider,
                    "user_not_found",
                    identity.Issuer,
                    identity.Email);

                return new LoginResult(false, null, GenericError, UserNotRegisteredErrorCode);
            }

            var existingUserLinks = await externalIdentityRepository.ListByUserEmailAsync(user.Email, cancellationToken);
            if (existingUserLinks.Any(link =>
                string.Equals(link.Provider, identity.Provider, StringComparison.OrdinalIgnoreCase)))
            {
                logger.LogWarning(
                    "SSO authentication rejected. Provider={Provider} FailureCategory={FailureCategory} Issuer={Issuer} Email={Email}",
                    identity.Provider,
                    "identity_mismatch",
                    identity.Issuer,
                    identity.Email);

                return new LoginResult(false, null, GenericError);
            }

            linkedIdentity = new ExternalIdentity
            {
                Provider = identity.Provider,
                Issuer = identity.Issuer,
                Subject = identity.Subject,
                UserEmail = user.Email,
                CreatedAt = dateTime.NowUtc,
                UpdatedAt = dateTime.NowUtc,
                LastLoginAt = dateTime.NowUtc
            };

            await externalIdentityRepository.AddAsync(linkedIdentity, cancellationToken);
        }

        linkedIdentity.LastLoginAt = dateTime.NowUtc;
        linkedIdentity.UpdatedAt = dateTime.NowUtc;
        await externalIdentityRepository.UpdateAsync(linkedIdentity, cancellationToken);

        var token = jwtTokenService.GenerateToken(user);
        var expiresAt = DateTime.UtcNow.AddHours(24);
        var response = new LoginResponse(
            token,
            user.Email,
            user.Role.ToString().ToLowerInvariant(),
            expiresAt);

        return new LoginResult(true, response, null);
    }
}
