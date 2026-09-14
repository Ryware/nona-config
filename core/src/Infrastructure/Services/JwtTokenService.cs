using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Nona.Domain.Entities;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Globalization;
using System.Text;

namespace Nona.Infrastructure.Services;

public class JwtTokenService(IConfiguration configuration) : IJwtTokenService
{
    public const string CredentialStampClaim = "nona_credential_stamp";

    // Keyed binding: never expose the stored password hash in a readable JWT payload.
    // Password writes automatically invalidate sessions without timestamp precision races.
    public static string GetCredentialStamp(User user, string signingKey)
    {
        var createdAt = user.CreatedAt.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(user.CreatedAt, DateTimeKind.Utc) : user.CreatedAt.ToUniversalTime();
        var state = string.Join("\n", "nona-session-v1",
            user.Id.ToString(CultureInfo.InvariantCulture),
            createdAt.Ticks.ToString(CultureInfo.InvariantCulture),
            user.PasswordHash ?? "", user.PasswordSalt ?? "");
        return Convert.ToHexString(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(signingKey), Encoding.UTF8.GetBytes(state)));
    }

    public string GenerateToken(User user)
    {
        var key = configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key is not configured");
        var issuer = configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("JWT Issuer is not configured");
        var audience = configuration["Jwt:Audience"] ?? throw new InvalidOperationException("JWT Audience is not configured");

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Email),
                new Claim(ClaimTypes.Name, user.Email),
                new Claim(ClaimTypes.Role, user.Role.ToString()),
                new Claim(CredentialStampClaim, GetCredentialStamp(user, key))
            ]),
            Expires = DateTime.UtcNow.AddHours(24),
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = credentials
        };

        var handler = new JsonWebTokenHandler();
        return handler.CreateToken(tokenDescriptor);
    }
}
