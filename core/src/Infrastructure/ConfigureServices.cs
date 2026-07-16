global using Nona.Application.Common.Interfaces;
global using Nona.Infrastructure.Authentication;
global using Nona.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nona.Domain.Interfaces;
using Nona.Infrastructure.Configuration;
using Nona.Infrastructure.Repositories.InMemory;
using Nona.Infrastructure.Repositories.Libsql;
using Nona.Libsql;

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

        ConfigurePersistence(services, configuration);

        return services;
    }

    private static void ConfigurePersistence(IServiceCollection services, IConfiguration configuration)
    {
        var storageType = ConfigurationValueReader.GetString(configuration, "Storage:Type", "InMemory");

        if (storageType.Equals("Libsql", StringComparison.OrdinalIgnoreCase))
        {
            ConfigureLibsqlPersistence(services, configuration);
        }
        else
        {
            ConfigureInMemoryPersistence(services);
        }
    }

    private static void ConfigureInMemoryPersistence(IServiceCollection services)
    {
        services.AddSingleton<IAuditLogRepository, InMemoryAuditLogRepository>();
        services.AddSingleton<IUserRepository, InMemoryUserRepository>();
        services.AddSingleton<IExternalIdentityRepository, InMemoryExternalIdentityRepository>();
        services.AddSingleton<IProjectRepository, InMemoryProjectRepository>();
        services.AddSingleton<IApiKeyRepository, InMemoryApiKeyRepository>();
        services.AddSingleton<IEnvironmentRepository, InMemoryEnvironmentRepository>();
        services.AddSingleton<IConfigEntryRepository, InMemoryConfigEntryRepository>();
        services.AddSingleton<IConfigReleaseRepository, InMemoryConfigReleaseRepository>();
        services.AddSingleton<IProjectMemberRepository, InMemoryProjectMemberRepository>();
        services.AddSingleton<IParameterShareLinkRepository, InMemoryParameterShareLinkRepository>();
    }

    private static void ConfigureLibsqlPersistence(
        IServiceCollection services,
        IConfiguration configuration)
    {
        var managedPrimary = new LibsqlManagedPrimaryOptions
        {
            Enabled = ConfigurationValueReader.GetBoolean(configuration, "Storage:Libsql:ManagedPrimary:Enabled"),
            ExecutablePath = configuration["Storage:Libsql:ManagedPrimary:ExecutablePath"] ?? "sqld",
            DatabasePath = configuration["Storage:Libsql:ManagedPrimary:DatabasePath"] ?? "nona-config-primary.db",
            HttpListenAddress = configuration["Storage:Libsql:ManagedPrimary:HttpListenAddress"] ?? "127.0.0.1:9080",
            LocalConnectUrl = configuration["Storage:Libsql:ManagedPrimary:LocalConnectUrl"] ?? string.Empty,
            WorkingDirectory = configuration["Storage:Libsql:ManagedPrimary:WorkingDirectory"] ?? string.Empty,
            StartTimeoutSeconds = ConfigurationValueReader.GetInt32(configuration, "Storage:Libsql:ManagedPrimary:StartTimeoutSeconds", 30),
            ExtraArgs = ConfigurationValueReader.GetStringList(configuration, "Storage:Libsql:ManagedPrimary:ExtraArgs").ToArray()
        };

        var dataSource = managedPrimary.Enabled
            ? managedPrimary.ResolveLocalConnectUrl()
            : configuration["ConnectionStrings:Libsql"]
                ?? configuration["Storage:Libsql:DataSource"];
        var authToken = configuration["Storage:Libsql:AuthToken"];
        var timeoutSeconds = ConfigurationValueReader.GetInt32(configuration, "Storage:Libsql:TimeoutSeconds", 30);
        var enableLocalReplica = ConfigurationValueReader.GetBoolean(configuration, "Storage:Libsql:EnableLocalReplica");
        var localReplicaPath = configuration["Storage:Libsql:LocalReplicaPath"];
        var localReplicaSyncIntervalSeconds = ConfigurationValueReader.GetDouble(configuration, "Storage:Libsql:LocalReplicaSyncIntervalSeconds", 1);

        services.AddOptions<LibsqlOptions>()
            .Configure(options =>
            {
                options.DataSource = dataSource ?? string.Empty;
                options.AuthToken = authToken ?? string.Empty;
                options.TimeoutSeconds = timeoutSeconds;
                options.EnableLocalReplica = enableLocalReplica;
                options.LocalReplicaPath = localReplicaPath ?? string.Empty;
                options.LocalReplicaSyncIntervalSeconds = localReplicaSyncIntervalSeconds;
                options.ManagedPrimary = managedPrimary;
            })
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.DataSource),
                "Storage:Libsql:DataSource or ConnectionStrings:Libsql must be configured.")
            .Validate(
                options => IsLibsqlHttpDataSource(options.DataSource),
                "Nona requires a sqld/libSQL HTTP data source. Enable Storage:Libsql:ManagedPrimary or configure http(s):// / libsql://.")
            .Validate(options => options.TimeoutSeconds > 0, "Storage:Libsql:TimeoutSeconds must be greater than zero.")
            .Validate(
                options => !options.EnableLocalReplica,
                "Storage:Libsql:EnableLocalReplica is not supported. Use managed sqld replica options such as --primary-grpc-url.")
            .Validate(
                options => !options.ManagedPrimary.Enabled || !string.IsNullOrWhiteSpace(options.ManagedPrimary.ExecutablePath),
                "Storage:Libsql:ManagedPrimary:ExecutablePath must be configured when Storage:Libsql:ManagedPrimary:Enabled is true.")
            .Validate(
                options => !options.ManagedPrimary.Enabled || !string.IsNullOrWhiteSpace(options.ManagedPrimary.DatabasePath),
                "Storage:Libsql:ManagedPrimary:DatabasePath must be configured when Storage:Libsql:ManagedPrimary:Enabled is true.")
            .Validate(
                options => !options.ManagedPrimary.Enabled || !string.IsNullOrWhiteSpace(options.ManagedPrimary.HttpListenAddress),
                "Storage:Libsql:ManagedPrimary:HttpListenAddress must be configured when Storage:Libsql:ManagedPrimary:Enabled is true.")
            .Validate(
                options => !options.ManagedPrimary.Enabled || options.ManagedPrimary.StartTimeoutSeconds > 0,
                "Storage:Libsql:ManagedPrimary:StartTimeoutSeconds must be greater than zero when Storage:Libsql:ManagedPrimary:Enabled is true.")
            .ValidateOnStart();

        services.AddSingleton<NelknetLibsqlDatabaseClient>();
        services.AddSingleton<ILibsqlDatabaseClient>(sp => sp.GetRequiredService<NelknetLibsqlDatabaseClient>());

        services.AddHostedService<ManagedLibsqlPrimaryHostedService>();
        services.AddHostedService<LibsqlDatabaseInitializer>();

        services.AddSingleton<IAuditLogRepository, LibsqlAuditLogRepository>();
        services.AddSingleton<IConfigEntryRepository, LibsqlConfigEntryRepository>();
        services.AddSingleton<IConfigReleaseRepository, LibsqlConfigReleaseRepository>();
        services.AddSingleton<IUserRepository, LibsqlUserRepository>();
        services.AddSingleton<IExternalIdentityRepository, LibsqlExternalIdentityRepository>();
        services.AddSingleton<IProjectRepository, LibsqlProjectRepository>();
        services.AddSingleton<IApiKeyRepository, LibsqlApiKeyRepository>();
        services.AddSingleton<IEnvironmentRepository, LibsqlEnvironmentRepository>();
        services.AddSingleton<IProjectMemberRepository, LibsqlProjectMemberRepository>();
        services.AddSingleton<IParameterShareLinkRepository, LibsqlParameterShareLinkRepository>();
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

    private static bool IsLibsqlHttpDataSource(string dataSource)
    {
        return dataSource.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || dataSource.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || dataSource.StartsWith("libsql://", StringComparison.OrdinalIgnoreCase);
    }
}
