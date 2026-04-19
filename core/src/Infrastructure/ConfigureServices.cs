global using Nona.Application.Common.Interfaces;
global using Nona.Infrastructure.Authentication;
global using Nona.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Nona.Domain.Interfaces;
using Nona.Infrastructure.Repositories.InMemory;
using Nona.Infrastructure.Repositories.Libsql;
using Nona.Infrastructure.Repositories.Sqlite;
using Nona.Libsql;
using System.Net.Http.Headers;

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
        var storageType = configuration.GetValue<string>("Storage:Type") ?? "InMemory";

        if (storageType.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            ConfigureSqlitePersistence(services, configuration);
        }
        else if (storageType.Equals("Libsql", StringComparison.OrdinalIgnoreCase))
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
        services.AddSingleton<IEnvironmentRepository, InMemoryEnvironmentRepository>();
        services.AddSingleton<IConfigEntryRepository, InMemoryConfigEntryRepository>();
        services.AddSingleton<IProjectMemberRepository, InMemoryProjectMemberRepository>();
    }

    private static void ConfigureSqlitePersistence(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Sqlite")
            ?? "Data Source=nona-config.db";

        services.AddSingleton(sp => new SqliteDbContext(connectionString));
        services.AddHostedService<SqliteDatabaseInitializer>();

        services.AddSingleton<IAuditLogRepository, SqliteAuditLogRepository>();
        services.AddSingleton<IConfigEntryRepository, SqliteConfigEntryRepository>();
        services.AddSingleton<IUserRepository, SqliteUserRepository>();
        services.AddSingleton<IExternalIdentityRepository, SqliteExternalIdentityRepository>();
        services.AddSingleton<IProjectRepository, SqliteProjectRepository>();
        services.AddSingleton<IEnvironmentRepository, SqliteEnvironmentRepository>();
        services.AddSingleton<IProjectMemberRepository, SqliteProjectMemberRepository>();
    }

    private static void ConfigureLibsqlPersistence(IServiceCollection services, IConfiguration configuration)
    {
        var url = configuration.GetConnectionString("Libsql")
            ?? configuration["Storage:Libsql:Url"];
        var authToken = configuration["Storage:Libsql:AuthToken"];
        var timeoutSeconds = configuration.GetValue<int?>("Storage:Libsql:TimeoutSeconds") ?? 30;
        var enableLocalReplica = configuration.GetValue<bool>("Storage:Libsql:EnableLocalReplica");
        var localReplicaPath = configuration["Storage:Libsql:LocalReplicaPath"];
        var localReplicaRole = configuration["Storage:Libsql:LocalReplicaRole"] ?? "Replica";

        services.AddOptions<LibsqlOptions>()
            .Configure(options =>
            {
                options.Url = url ?? string.Empty;
                options.AuthToken = authToken ?? string.Empty;
                options.TimeoutSeconds = timeoutSeconds;
                options.EnableLocalReplica = enableLocalReplica;
                options.LocalReplicaPath = localReplicaPath ?? string.Empty;
                options.LocalReplicaRole = localReplicaRole;
            })
            .Validate(
                options => !options.EnableLocalReplica || IsValidLocalReplicaRole(options.LocalReplicaRole),
                "Storage:Libsql:LocalReplicaRole must be either 'Primary' or 'Replica' when Storage:Libsql:EnableLocalReplica is true.")
            .Validate(
                options => !RequiresRemotePeer(options) || !string.IsNullOrWhiteSpace(options.Url),
                "Storage:Libsql:Url or ConnectionStrings:Libsql must be configured for direct libSQL or replica mode.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.AuthToken), "Storage:Libsql:AuthToken must be configured.")
            .Validate(options => options.TimeoutSeconds > 0, "Storage:Libsql:TimeoutSeconds must be greater than zero.")
            .Validate(
                options => !options.EnableLocalReplica || !string.IsNullOrWhiteSpace(options.LocalReplicaPath),
                "Storage:Libsql:LocalReplicaPath must be configured when Storage:Libsql:EnableLocalReplica is true.")
            .ValidateOnStart();

        services.AddHttpClient<LibsqlHttpDatabaseClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<LibsqlOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.AuthToken);

            if (!string.IsNullOrWhiteSpace(options.Url))
            {
                client.BaseAddress = new Uri($"{LibsqlHttpDatabaseClient.NormalizeBaseUrl(options.Url)}/");
            }
        });

        if (enableLocalReplica)
        {
            services.AddSingleton<LibsqlMirroredLocalDatabaseClient>();
            services.AddSingleton<ILibsqlDatabaseClient>(sp => sp.GetRequiredService<LibsqlMirroredLocalDatabaseClient>());
        }
        else
        {
            services.AddSingleton<ILibsqlDatabaseClient>(sp => sp.GetRequiredService<LibsqlHttpDatabaseClient>());
        }

        services.AddHostedService<LibsqlDatabaseInitializer>();

        services.AddSingleton<IAuditLogRepository, LibsqlAuditLogRepository>();
        services.AddSingleton<IConfigEntryRepository, LibsqlConfigEntryRepository>();
        services.AddSingleton<IUserRepository, LibsqlUserRepository>();
        services.AddSingleton<IExternalIdentityRepository, LibsqlExternalIdentityRepository>();
        services.AddSingleton<IProjectRepository, LibsqlProjectRepository>();
        services.AddSingleton<IEnvironmentRepository, LibsqlEnvironmentRepository>();
        services.AddSingleton<IProjectMemberRepository, LibsqlProjectMemberRepository>();
    }

    private static void ConfigureSsoOptions(SsoOptions options, IConfiguration configuration)
    {
        options.Google.ClientId = configuration["Sso:Google:ClientId"]
            ?? string.Empty;
        options.Google.JwksUri = configuration["Sso:Google:JwksUri"]
            ?? options.Google.JwksUri;

        var googleIssuers = configuration.GetSection("Sso:Google:Issuers").Get<string[]>();
        if (googleIssuers is { Length: > 0 })
        {
            options.Google.Issuers = googleIssuers.ToList();
        }

        options.Microsoft.ClientId = configuration["Sso:Microsoft:ClientId"]
            ?? string.Empty;
        options.Microsoft.TenantId = configuration["Sso:Microsoft:TenantId"]
            ?? "common";
        options.Microsoft.JwksUri = configuration["Sso:Microsoft:JwksUri"]
            ?? options.Microsoft.JwksUri;

        var microsoftIssuers = configuration.GetSection("Sso:Microsoft:Issuers").Get<string[]>();
        if (microsoftIssuers is { Length: > 0 })
        {
            options.Microsoft.Issuers = microsoftIssuers.ToList();
        }
    }

    private static bool RequiresRemotePeer(LibsqlOptions options)
    {
        return !options.EnableLocalReplica || !IsPrimaryLocalReplica(options.LocalReplicaRole);
    }

    private static bool IsValidLocalReplicaRole(string role)
    {
        return role.Equals("Primary", StringComparison.OrdinalIgnoreCase)
            || role.Equals("Replica", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPrimaryLocalReplica(string role)
    {
        return role.Equals("Primary", StringComparison.OrdinalIgnoreCase);
    }
}
