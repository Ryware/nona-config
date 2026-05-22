using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Nona.Infrastructure.Authentication;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;

namespace Nona.Infrastructure.Tests;

public class SsoTokenValidatorTests
{
    [Test]
    public async Task GoogleValidator_AcceptsValidToken()
    {
        using var signingKey = new SigningKeyFixture();
        var validator = CreateGoogleValidator(signingKey);
        var token = signingKey.CreateToken(
            issuer: "https://accounts.google.com",
            audience: "google-client-id",
            claims:
            [
                new Claim("sub", "google-user-1"),
                new Claim("email", "user@example.com"),
                new Claim("name", "Example User"),
                new Claim("email_verified", "true")
            ]);

        var result = await validator.ValidateAsync(token);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Identity).IsNotNull();
        await Assert.That(result.Identity!.Email).IsEqualTo("user@example.com");
    }

    [Test]
    public async Task GoogleValidator_RejectsInvalidIssuer()
    {
        using var signingKey = new SigningKeyFixture();
        var validator = CreateGoogleValidator(signingKey);
        var token = signingKey.CreateToken(
            issuer: "https://issuer.example",
            audience: "google-client-id",
            claims:
            [
                new Claim("sub", "google-user-1"),
                new Claim("email", "user@example.com"),
                new Claim("email_verified", "true")
            ]);

        var result = await validator.ValidateAsync(token);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.FailureCategory).IsEqualTo("invalid_issuer");
    }

    [Test]
    public async Task GoogleValidator_RejectsInvalidAudience()
    {
        using var signingKey = new SigningKeyFixture();
        var validator = CreateGoogleValidator(signingKey);
        var token = signingKey.CreateToken(
            issuer: "https://accounts.google.com",
            audience: "wrong-audience",
            claims:
            [
                new Claim("sub", "google-user-1"),
                new Claim("email", "user@example.com"),
                new Claim("email_verified", "true")
            ]);

        var result = await validator.ValidateAsync(token);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.FailureCategory).IsEqualTo("invalid_token");
    }

    [Test]
    public async Task MicrosoftValidator_AcceptsValidToken()
    {
        using var signingKey = new SigningKeyFixture();
        var validator = CreateMicrosoftValidator(signingKey, tenantId: "common");
        var token = signingKey.CreateToken(
            issuer: "https://login.microsoftonline.com/tenant-123/v2.0",
            audience: "microsoft-client-id",
            claims:
            [
                new Claim("sub", "microsoft-user-1"),
                new Claim("tid", "tenant-123"),
                new Claim("preferred_username", "user@example.com"),
                new Claim("name", "Example User")
            ]);

        var result = await validator.ValidateAsync(token);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Identity).IsNotNull();
        await Assert.That(result.Identity!.TenantId).IsEqualTo("tenant-123");
    }

    [Test]
    public async Task MicrosoftValidator_RejectsDisallowedTenant()
    {
        using var signingKey = new SigningKeyFixture();
        var validator = CreateMicrosoftValidator(signingKey, tenantId: "allowed-tenant");
        var token = signingKey.CreateToken(
            issuer: "https://login.microsoftonline.com/other-tenant/v2.0",
            audience: "microsoft-client-id",
            claims:
            [
                new Claim("sub", "microsoft-user-1"),
                new Claim("tid", "other-tenant"),
                new Claim("preferred_username", "user@example.com")
            ]);

        var result = await validator.ValidateAsync(token);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.FailureCategory).IsEqualTo("invalid_tenant");
    }

    private static GoogleSsoTokenValidator CreateGoogleValidator(SigningKeyFixture signingKey)
    {
        var options = Options.Create(new SsoOptions
        {
            Google = new GoogleSsoOptions
            {
                ClientId = "google-client-id",
                JwksUri = "https://jwks.test/google",
                Issuers = ["https://accounts.google.com", "accounts.google.com"]
            }
        });

        return new GoogleSsoTokenValidator(CreateSigningKeyCache(signingKey, "https://jwks.test/google"), options);
    }

    private static MicrosoftSsoTokenValidator CreateMicrosoftValidator(SigningKeyFixture signingKey, string tenantId)
    {
        var options = Options.Create(new SsoOptions
        {
            Microsoft = new MicrosoftSsoOptions
            {
                ClientId = "microsoft-client-id",
                TenantId = tenantId,
                JwksUri = "https://jwks.test/microsoft"
            }
        });

        return new MicrosoftSsoTokenValidator(CreateSigningKeyCache(signingKey, "https://jwks.test/microsoft"), options);
    }

    private static JwksSigningKeyCache CreateSigningKeyCache(SigningKeyFixture signingKey, string jwksUri)
    {
        var handler = new StaticJsonHttpMessageHandler(jwksUri, signingKey.CreateJwksDocument());
        var httpClient = new HttpClient(handler);
        return new JwksSigningKeyCache(new FixedHttpClientFactory(httpClient));
    }

    private sealed class StaticJsonHttpMessageHandler(string expectedUri, string content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (!string.Equals(request.RequestUri?.ToString(), expectedUri, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content)
            });
        }
    }

    private sealed class FixedHttpClientFactory(HttpClient httpClient) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => httpClient;
    }

    private sealed class SigningKeyFixture : IDisposable
    {
        private readonly RSA _rsa = RSA.Create(2048);

        public SigningKeyFixture()
        {
            SigningKey = new RsaSecurityKey(_rsa)
            {
                KeyId = "test-key"
            };
        }

        public RsaSecurityKey SigningKey { get; }

        public string CreateToken(string issuer, string audience, IEnumerable<Claim> claims)
        {
            var descriptor = new SecurityTokenDescriptor
            {
                Issuer = issuer,
                Audience = audience,
                Expires = DateTime.UtcNow.AddMinutes(10),
                Subject = new ClaimsIdentity(claims),
                SigningCredentials = new SigningCredentials(SigningKey, SecurityAlgorithms.RsaSha256)
            };

            return new JsonWebTokenHandler().CreateToken(descriptor);
        }

        public string CreateJwksDocument()
        {
            var parameters = _rsa.ExportParameters(false);

            return $$"""
            {
              "keys": [
                {
                  "kty": "RSA",
                  "use": "sig",
                  "alg": "RS256",
                  "kid": "{{SigningKey.KeyId}}",
                  "n": "{{Base64UrlEncoder.Encode(parameters.Modulus)}}",
                  "e": "{{Base64UrlEncoder.Encode(parameters.Exponent)}}"
                }
              ]
            }
            """;
        }

        public void Dispose()
        {
            _rsa.Dispose();
        }
    }
}
