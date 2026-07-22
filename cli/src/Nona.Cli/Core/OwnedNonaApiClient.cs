using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using Nona.Cli.Generated;

namespace Nona.Cli;

internal sealed class OwnedNonaApiClient : NonaApiClient, IDisposable
{
    private readonly HttpClientRequestAdapter _adapter;
    private readonly HttpClient _httpClient;
    private int _disposed;

    internal OwnedNonaApiClient(IAuthenticationProvider authProvider, HttpClient httpClient)
        : this(new HttpClientRequestAdapter(authProvider, httpClient: httpClient), httpClient)
    {
    }

    private OwnedNonaApiClient(HttpClientRequestAdapter adapter, HttpClient httpClient)
        : base(adapter)
    {
        _adapter = adapter;
        _httpClient = httpClient;
    }

    internal string? BaseUrl
    {
        get => _adapter.BaseUrl;
        set => _adapter.BaseUrl = value;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        try
        {
            _adapter.Dispose();
        }
        finally
        {
            _httpClient.Dispose();
        }
    }
}
