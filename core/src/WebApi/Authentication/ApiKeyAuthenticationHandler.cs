using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Nona.WebApi.Endpoints;
using Nona.Application.Common;

namespace Nona.WebApi.Authentication;

public class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApiKey";
    public const string ApiKeyHeaderName = "X-Api-Key";
    internal const string InvalidCredentialDetail = "An API key is required or invalid.";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiKeyHeaderName, out var apiKeyHeader))
            return Task.FromResult(AuthenticateResult.Fail("API key header not found"));

        if (apiKeyHeader.Count != 1 || !IsCanonicalApiKey(apiKeyHeader[0]))
            return Task.FromResult(AuthenticateResult.Fail("API key is malformed"));

        var apiKey = apiKeyHeader[0]!;

        // Keep only the digest in claims; project and environment validation happens in the query handler.
        var claims = new[]
        {
            new Claim("ApiKeyHash", ApiKeySecret.Hash(apiKey))
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        => ApiProblemResults
            .Unauthorized(InvalidCredentialDetail)
            .ExecuteAsync(Context);

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
        => ApiProblemResults
            .Forbidden("The API key does not grant access to this resource.")
            .ExecuteAsync(Context);

    private static bool IsCanonicalApiKey(string? value)
        => value is { Length: 64 }
           && value.All(character => character is >= '0' and <= '9' or >= 'A' and <= 'F');
}
