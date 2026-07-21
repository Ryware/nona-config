global using Nona.Application.Common.Interfaces;
global using Nona.Infrastructure.Authentication;
global using Nona.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nona.Infrastructure.Configuration;

namespace Nona.Infrastructure;

public static class ConfigureServices
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient("SsoJwks", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        services.AddOptions<SsoOptions>()
            .Configure(options => ConfigureSsoOptions(options, configuration));

        services.AddSingleton<IGuidGenerator, GuidGeneratorService>();
        services.AddSingleton<IDateTime, DateTimeService>();
        services.AddSingleton<IRandom, RandomService>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<JwksSigningKeyCache>();
        services.AddSingleton<ISsoTokenValidator, GoogleSsoTokenValidator>();
        services.AddSingleton<ISsoTokenValidator, MicrosoftSsoTokenValidator>();
        services.AddSingleton<ISsoPublicConfigurationProvider, SsoPublicConfigurationProvider>();
        services.AddScoped<IAuditLogService, AuditLogService>();

        services.AddStorageProvider(configuration);

        return services;
    }

    private static void ConfigureSsoOptions(SsoOptions options, IConfiguration configuration)
    {
        options.Google.ClientId = configuration["Sso:Google:ClientId"]
            ?? string.Empty;
        options.Google.JwksUri = configuration["Sso:Google:JwksUri"]
            ?? options.Google.JwksUri;

        var googleIssuers = ConfigurationValueReader.GetStringList(configuration, "Sso:Google:Issuers");
        if (googleIssuers.Count > 0)
        {
            options.Google.Issuers = googleIssuers.ToList();
        }

        options.Microsoft.ClientId = configuration["Sso:Microsoft:ClientId"]
            ?? string.Empty;
        options.Microsoft.TenantId = configuration["Sso:Microsoft:TenantId"]
            ?? "common";
        options.Microsoft.JwksUri = configuration["Sso:Microsoft:JwksUri"]
            ?? options.Microsoft.JwksUri;

        var microsoftIssuers = ConfigurationValueReader.GetStringList(configuration, "Sso:Microsoft:Issuers");
        if (microsoftIssuers.Count > 0)
        {
            options.Microsoft.Issuers = microsoftIssuers.ToList();
        }
    }

}
