using Microsoft.Kiota.Abstractions.Authentication;
using Nona.Migrator.Core.Services;

namespace Nona.Cli;

internal static class NonaClientFactory
{
    internal static OwnedNonaApiClient Create(
        NonaCliConnectionOptions connection,
        Func<HttpClient>? httpClientFactory = null)
    {
        var authProvider = new BaseBearerTokenAuthenticationProvider(
            new StaticTokenProvider(connection.BearerToken!));

        var httpClient = httpClientFactory is null
            ? NonaApiHttpClientFactory.Create()
            : httpClientFactory();
        OwnedNonaApiClient? client = null;
        var ownershipTransferred = false;

        try
        {
            client = new OwnedNonaApiClient(authProvider, httpClient);
            ownershipTransferred = true;
            client.BaseUrl = connection.BaseUrl.TrimEnd('/');
            return client;
        }
        catch
        {
            client?.Dispose();
            throw;
        }
        finally
        {
            if (!ownershipTransferred)
                httpClient.Dispose();
        }
    }

    private sealed class StaticTokenProvider(string token) : IAccessTokenProvider
    {
        public Task<string> GetAuthorizationTokenAsync(
            Uri uri,
            Dictionary<string, object>? additionalAuthenticationContext = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(token);

        public AllowedHostsValidator AllowedHostsValidator { get; } = new();
    }
}
