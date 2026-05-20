using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Nona.Application.Auth;
using Nona.Application.Common.Interfaces;

namespace Nona.Infrastructure.Authentication;

public sealed class GoogleSsoTokenValidator(
    JwksSigningKeyCache signingKeyCache,
    IOptions<SsoOptions> options)
    : SsoTokenValidatorBase(signingKeyCache), ISsoTokenValidator
{
    private readonly GoogleSsoOptions _googleOptions = options.Value.Google;

    public string Provider => SsoProviders.Google;
    public bool IsEnabled => !string.IsNullOrWhiteSpace(_googleOptions.ClientId);

    public async Task<SsoTokenValidationResult> ValidateAsync(string idToken, CancellationToken ct = default)
    {
        if (!IsEnabled)
        {
            return SsoTokenValidationResult.Failed("provider_disabled");
        }

        if (string.IsNullOrWhiteSpace(idToken))
        {
            return SsoTokenValidationResult.Failed("token_missing");
        }

        var validation = await ValidateSignedTokenAsync(idToken, _googleOptions.ClientId, _googleOptions.JwksUri, ct);
        if (!validation.IsValid || validation.SecurityToken is not JsonWebToken token)
        {
            return SsoTokenValidationResult.Failed("invalid_token");
        }

        if (!_googleOptions.Issuers.Any(issuer => string.Equals(issuer, token.Issuer, StringComparison.OrdinalIgnoreCase)))
        {
            return SsoTokenValidationResult.Failed("invalid_issuer");
        }

        var email = GetClaim(token, "email");
        if (string.IsNullOrWhiteSpace(email))
        {
            return SsoTokenValidationResult.Failed("email_missing");
        }

        if (!TryGetBooleanClaim(token, "email_verified", out var emailVerified) || !emailVerified)
        {
            return SsoTokenValidationResult.Failed("email_not_verified");
        }

        return SsoTokenValidationResult.Succeeded(new SsoIdentity(
            Provider,
            token.Subject,
            token.Issuer,
            email,
            GetClaim(token, "name"),
            null,
            true));
    }
}
