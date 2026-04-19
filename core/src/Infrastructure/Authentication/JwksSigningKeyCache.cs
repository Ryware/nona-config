using Microsoft.IdentityModel.Tokens;
using System.Collections.Concurrent;

namespace Nona.Infrastructure.Authentication;

public sealed class JwksSigningKeyCache(IHttpClientFactory httpClientFactory)
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<IReadOnlyCollection<SecurityKey>> GetSigningKeysAsync(string jwksUri, CancellationToken ct)
    {
        if (_cache.TryGetValue(jwksUri, out var cached) && cached.ExpiresAt > DateTimeOffset.UtcNow)
        {
            return cached.Keys;
        }

        await _gate.WaitAsync(ct);
        try
        {
            if (_cache.TryGetValue(jwksUri, out cached) && cached.ExpiresAt > DateTimeOffset.UtcNow)
            {
                return cached.Keys;
            }

            var client = httpClientFactory.CreateClient("SsoJwks");
            using var response = await client.GetAsync(jwksUri, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            var keySet = new JsonWebKeySet(json);
            var keys = keySet.GetSigningKeys().ToList();

            if (keys.Count == 0)
            {
                throw new InvalidOperationException($"JWKS document at '{jwksUri}' did not contain any signing keys.");
            }

            cached = new CacheEntry(keys, DateTimeOffset.UtcNow.AddMinutes(15));
            _cache[jwksUri] = cached;
            return cached.Keys;
        }
        finally
        {
            _gate.Release();
        }
    }

    private sealed record CacheEntry(IReadOnlyCollection<SecurityKey> Keys, DateTimeOffset ExpiresAt);
}
