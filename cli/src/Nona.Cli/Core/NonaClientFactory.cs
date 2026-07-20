using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using Nona.Cli.Generated;
using Nona.Migrator.Core.Services;

namespace Nona.Cli;

internal static class NonaClientFactory
{
    internal static NonaApiClient Create(NonaCliConnectionOptions connection, Func<HttpClient>? httpClientFactory = null)
    {
        var authProvider = new BaseBearerTokenAuthenticationProvider(
            new StaticTokenProvider(connection.BearerToken!));

        var adapter = httpClientFactory is null
            ? new HttpClientRequestAdapter(authProvider, httpClient: NonaApiHttpClientFactory.Create())
            : new HttpClientRequestAdapter(authProvider, httpClient: httpClientFactory());

        adapter.BaseUrl = connection.BaseUrl.TrimEnd('/');

        return new NonaApiClient(adapter);
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
