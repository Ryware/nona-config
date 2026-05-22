using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Nona.Infrastructure.Authentication;

public abstract class SsoTokenValidatorBase(JwksSigningKeyCache signingKeyCache)
{
    protected async Task<TokenValidationResult> ValidateSignedTokenAsync(
        string idToken,
        string audience,
        string jwksUri,
        CancellationToken ct)
    {
        var signingKeys = await signingKeyCache.GetSigningKeysAsync(jwksUri, ct);
        var handler = new JsonWebTokenHandler();

        return await handler.ValidateTokenAsync(idToken, new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            RequireSignedTokens = true,
            IssuerSigningKeys = signingKeys,
            ClockSkew = TimeSpan.FromMinutes(1)
        });
    }

    protected static string? GetClaim(JsonWebToken token, string claimType)
    {
        return token.Claims.FirstOrDefault(claim => claim.Type == claimType)?.Value;
    }

    protected static bool TryGetBooleanClaim(JsonWebToken token, string claimType, out bool value)
    {
        var raw = GetClaim(token, claimType);
        if (bool.TryParse(raw, out value))
        {
            return true;
        }

        if (string.Equals(raw, "1", StringComparison.OrdinalIgnoreCase))
        {
            value = true;
            return true;
        }

        if (string.Equals(raw, "0", StringComparison.OrdinalIgnoreCase))
        {
            value = false;
            return true;
        }

        value = false;
        return false;
    }
}
