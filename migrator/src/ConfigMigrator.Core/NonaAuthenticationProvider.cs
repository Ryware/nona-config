using System.Net.Http.Json;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;
using Nona.Migrator.Core.Options;

namespace Nona.Migrator.Core;

/// <summary>
/// Kiota authentication provider that attaches a bearer token.
/// If only email/password are configured it calls POST /auth/login first.
/// </summary>
public sealed class NonaAuthenticationProvider(NonaOptions options) : IAuthenticationProvider
{
    private string? _cachedToken;

    public async Task AuthenticateRequestAsync(
        RequestInformation request,
        Dictionary<string, object>? additionalAuthenticationContext = null,
        CancellationToken cancellationToken = default)
    {
        var token = await GetTokenAsync(cancellationToken);
        request.Headers.Add("Authorization", $"Bearer {token}");
    }

    private async Task<string> GetTokenAsync(CancellationToken ct)
    {
        if (_cachedToken is not null)
            return _cachedToken;

        if (!string.IsNullOrWhiteSpace(options.BearerToken))
        {
            _cachedToken = options.BearerToken;
            return _cachedToken;
        }

        // Fall back to email/password login
        using var http = new HttpClient();
        var baseUrl = options.BaseUrl.TrimEnd('/');

        using var response = await http.PostAsJsonAsync(
            $"{baseUrl}/auth/login",
            new { email = options.Email, password = options.Password },
            ct);

        var content = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Nona login failed ({(int)response.StatusCode}): {content}");

        using var doc = System.Text.Json.JsonDocument.Parse(content);
        _cachedToken = doc.RootElement.GetProperty("token").GetString()
                       ?? throw new InvalidOperationException("Login response missing token.");

        return _cachedToken;
    }
}
