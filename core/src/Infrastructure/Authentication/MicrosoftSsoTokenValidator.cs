using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Nona.Application.Auth;
using Nona.Application.Common.Interfaces;

namespace Nona.Infrastructure.Authentication;

public sealed class MicrosoftSsoTokenValidator(
    JwksSigningKeyCache signingKeyCache,
    IOptions<SsoOptions> options)
    : SsoTokenValidatorBase(signingKeyCache), ISsoTokenValidator
{
    private readonly MicrosoftSsoOptions _microsoftOptions = options.Value.Microsoft;

    public string Provider => SsoProviders.Microsoft;
    public bool IsEnabled => !string.IsNullOrWhiteSpace(_microsoftOptions.ClientId);

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

        var validation = await ValidateSignedTokenAsync(idToken, _microsoftOptions.ClientId, _microsoftOptions.JwksUri, ct);
        if (!validation.IsValid || validation.SecurityToken is not JsonWebToken token)
        {
            return SsoTokenValidationResult.Failed("invalid_token");
        }

        var tenantId = GetClaim(token, "tid");
        var issuer = token.Issuer;

        if (_microsoftOptions.Issuers.Count > 0)
        {
            if (!_microsoftOptions.Issuers.Any(candidate => string.Equals(candidate, issuer, StringComparison.OrdinalIgnoreCase)))
            {
                return SsoTokenValidationResult.Failed("invalid_issuer");
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                return SsoTokenValidationResult.Failed("invalid_issuer");
            }

            var configuredTenant = string.IsNullOrWhiteSpace(_microsoftOptions.TenantId)
                ? "common"
                : _microsoftOptions.TenantId;

            if (!configuredTenant.Equals("common", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(configuredTenant, tenantId, StringComparison.OrdinalIgnoreCase))
            {
                return SsoTokenValidationResult.Failed("invalid_tenant");
            }

            var expectedTenant = configuredTenant.Equals("common", StringComparison.OrdinalIgnoreCase)
                ? tenantId
                : configuredTenant;
            var expectedIssuer = $"https://login.microsoftonline.com/{expectedTenant}/v2.0";

            if (!string.Equals(expectedIssuer, issuer, StringComparison.OrdinalIgnoreCase))
            {
                return SsoTokenValidationResult.Failed("invalid_issuer");
            }
        }

        var email = GetClaim(token, "email")
            ?? GetClaim(token, "preferred_username")
            ?? GetClaim(token, "upn");

        if (string.IsNullOrWhiteSpace(email))
        {
            return SsoTokenValidationResult.Failed("email_missing");
        }

        return SsoTokenValidationResult.Succeeded(new SsoIdentity(
            Provider,
            token.Subject,
            issuer,
            email,
            GetClaim(token, "name"),
            tenantId,
            true));
    }
}
