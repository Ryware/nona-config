using Microsoft.Extensions.Options;
using Nona.Application.Auth.DTOs;

namespace Nona.Infrastructure.Services;

public sealed class SsoPublicConfigurationProvider(IOptions<SsoOptions> options) : ISsoPublicConfigurationProvider
{
    private readonly SsoOptions _options = options.Value;

    public SsoPublicConfigResponse GetConfiguration()
    {
        var googleEnabled = !string.IsNullOrWhiteSpace(_options.Google.ClientId);
        var microsoftEnabled = !string.IsNullOrWhiteSpace(_options.Microsoft.ClientId);
        var tenantId = string.IsNullOrWhiteSpace(_options.Microsoft.TenantId) ? "common" : _options.Microsoft.TenantId;

        return new SsoPublicConfigResponse(
            new SsoProviderPublicConfig(
                googleEnabled,
                googleEnabled ? _options.Google.ClientId : null),
            new SsoProviderPublicConfig(
                microsoftEnabled,
                microsoftEnabled ? _options.Microsoft.ClientId : null,
                microsoftEnabled ? $"https://login.microsoftonline.com/{tenantId}" : null,
                microsoftEnabled ? tenantId : null));
    }
}
