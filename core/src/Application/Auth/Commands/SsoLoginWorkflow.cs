using Microsoft.Extensions.Logging;
using Nona.Application.Auth.DTOs;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Entities;
using Nona.Domain.Interfaces;

namespace Nona.Application.Auth.Commands;

internal static class SsoLoginWorkflow
{
    private const string GenericError = "Authentication failed";

    internal static async Task<LoginResult> AuthenticateAsync(
        string provider,
        string idToken,
        IEnumerable<ISsoTokenValidator> tokenValidators,
        IExternalIdentityRepository externalIdentityRepository,
        IUserRepository userRepository,
        IJwtTokenService jwtTokenService,
        IDateTime dateTime,
        ILogger logger,
        string? expectedEmail = null,
        CancellationToken cancellationToken = default)
    {
        var validator = tokenValidators.FirstOrDefault(candidate =>
            string.Equals(candidate.Provider, provider, StringComparison.OrdinalIgnoreCase));

        if (validator is null || !validator.IsEnabled)
        {
            logger.LogWarning(
                "SSO authentication rejected. Provider={Provider} FailureCategory={FailureCategory}",
                provider,
                "provider_disabled");

            return new LoginResult(false, null, GenericError);
        }

        var validation = await validator.ValidateAsync(idToken, cancellationToken);
        if (!validation.Success || validation.Identity is null)
        {
            logger.LogWarning(
                "SSO authentication rejected. Provider={Provider} FailureCategory={FailureCategory}",
                provider,
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

                return new LoginResult(false, null, GenericError, AuthErrorCodes.SsoUserNotRegistered);
            }

            if (IsExpectedEmailMismatch(expectedEmail, user.Email))
            {
                logger.LogWarning(
                    "SSO invitation rejected. Provider={Provider} FailureCategory={FailureCategory} Issuer={Issuer} ExpectedEmail={ExpectedEmail} ActualEmail={ActualEmail}",
                    identity.Provider,
                    "email_mismatch",
                    identity.Issuer,
                    expectedEmail,
                    user.Email);

                return new LoginResult(false, null, GenericError, AuthErrorCodes.InvitationSsoEmailMismatch);
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

            if (IsExpectedEmailMismatch(expectedEmail, identity.Email))
            {
                logger.LogWarning(
                    "SSO invitation rejected. Provider={Provider} FailureCategory={FailureCategory} Issuer={Issuer} ExpectedEmail={ExpectedEmail} ActualEmail={ActualEmail}",
                    identity.Provider,
                    "email_mismatch",
                    identity.Issuer,
                    expectedEmail,
                    identity.Email);

                return new LoginResult(false, null, GenericError, AuthErrorCodes.InvitationSsoEmailMismatch);
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

                return new LoginResult(false, null, GenericError, AuthErrorCodes.SsoUserNotRegistered);
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

            var now = dateTime.NowUtc;
            linkedIdentity = new ExternalIdentity
            {
                Provider = identity.Provider,
                Issuer = identity.Issuer,
                Subject = identity.Subject,
                UserEmail = user.Email,
                CreatedAt = now,
                UpdatedAt = now,
                LastLoginAt = now
            };

            await externalIdentityRepository.AddAsync(linkedIdentity, cancellationToken);
        }

        var currentTime = dateTime.NowUtc;
        linkedIdentity.LastLoginAt = currentTime;
        linkedIdentity.UpdatedAt = currentTime;
        await externalIdentityRepository.UpdateAsync(linkedIdentity, cancellationToken);

        if (!string.IsNullOrWhiteSpace(user.InviteTokenHash))
        {
            user.InviteTokenHash = null;
            user.UpdatedAt = currentTime;
            await userRepository.UpdateAsync(user, cancellationToken);
        }

        var token = jwtTokenService.GenerateToken(user);
        var expiresAt = DateTime.UtcNow.AddHours(24);
        var response = new LoginResponse(
            token,
            user.Email,
            user.Role.ToString().ToLowerInvariant(),
            expiresAt);

        return new LoginResult(true, response, null);
    }

    private static bool IsExpectedEmailMismatch(string? expectedEmail, string? actualEmail)
    {
        return !string.IsNullOrWhiteSpace(expectedEmail)
               && !string.Equals(expectedEmail, actualEmail, StringComparison.OrdinalIgnoreCase);
    }
}
