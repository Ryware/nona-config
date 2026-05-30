using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using Nona.Cli.Generated;

namespace Nona.Cli;

internal static class NonaClientFactory
{
    internal static NonaApiClient Create(NonaCliConnectionOptions connection)
    {
        var authProvider = new BaseBearerTokenAuthenticationProvider(
            new StaticTokenProvider(connection.BearerToken!));

        var adapter = new HttpClientRequestAdapter(authProvider)
        {
            BaseUrl = connection.BaseUrl.TrimEnd('/')
        };

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
