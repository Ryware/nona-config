using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nona.Domain.Interfaces;
using Nona.Infrastructure.Repositories.InMemory;
using Nona.Infrastructure.Repositories.Libsql;
using Nona.Infrastructure.Services;
using Nona.Libsql;

namespace Nona.Infrastructure.Configuration;

public static class StorageProviderServiceCollectionExtensions
{
    public static IServiceCollection AddStorageProvider(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var resolution = StorageProviderResolver.Resolve(configuration);

        switch (resolution.Provider)
        {
            case StorageProviderKind.Sqlite:
                AddSqliteProvider(services, configuration);
                break;
            case StorageProviderKind.Libsql:
                AddLibsqlProvider(services, configuration, resolution);
                break;
            case StorageProviderKind.InMemory:
                AddInMemoryProvider(services);
                break;
            default:
                throw new InvalidOperationException($"Unsupported storage provider '{resolution.Provider}'.");
        }

        return services;
    }

    private static void AddInMemoryProvider(IServiceCollection services)
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

    private static void AddLibsqlProvider(
        IServiceCollection services,
        IConfiguration configuration,
        StorageProviderResolution resolution)
    {
        var managedPrimary = new LibsqlManagedPrimaryOptions
        {
            Enabled = resolution.UseManagedLibsql,
            ExecutablePath = configuration["Storage:Libsql:ManagedPrimary:ExecutablePath"] ?? "sqld",
            DatabasePath = configuration["Storage:Libsql:ManagedPrimary:DatabasePath"] ?? "nona-config-primary.db",
            HttpListenAddress = configuration["Storage:Libsql:ManagedPrimary:HttpListenAddress"] ?? "127.0.0.1:9080",
            LocalConnectUrl = configuration["Storage:Libsql:ManagedPrimary:LocalConnectUrl"] ?? string.Empty,
            WorkingDirectory = configuration["Storage:Libsql:ManagedPrimary:WorkingDirectory"] ?? string.Empty,
            StartTimeoutSeconds = ConfigurationValueReader.GetInt32(
                configuration,
                "Storage:Libsql:ManagedPrimary:StartTimeoutSeconds",
                30),
            ExtraArgs = ConfigurationValueReader.GetStringList(
                configuration,
                "Storage:Libsql:ManagedPrimary:ExtraArgs").ToArray()
        };

        var dataSource = managedPrimary.Enabled
            ? managedPrimary.ResolveLocalConnectUrl()
            : configuration["ConnectionStrings:Libsql"]
                ?? configuration["Storage:Libsql:DataSource"];
        var authToken = configuration["Storage:Libsql:AuthToken"];
        var timeoutSeconds = ConfigurationValueReader.GetInt32(
            configuration,
            "Storage:Libsql:TimeoutSeconds",
            30);
        var enableLocalReplica = ConfigurationValueReader.GetBoolean(
            configuration,
            "Storage:Libsql:EnableLocalReplica");
        var localReplicaPath = configuration["Storage:Libsql:LocalReplicaPath"];
        var localReplicaSyncIntervalSeconds = ConfigurationValueReader.GetDouble(
            configuration,
            "Storage:Libsql:LocalReplicaSyncIntervalSeconds",
            1);

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
                options => StorageProviderResolver.IsRemoteLibsqlDataSource(options.DataSource),
                "Nona requires a sqld/libSQL HTTP data source. Enable Storage:Libsql:ManagedPrimary or configure http(s):// / libsql://.")
            .Validate(
                options => options.TimeoutSeconds > 0,
                "Storage:Libsql:TimeoutSeconds must be greater than zero.")
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
        services.AddSingleton<ILibsqlDatabaseClient>(
            serviceProvider => serviceProvider.GetRequiredService<NelknetLibsqlDatabaseClient>());

        if (resolution.UseManagedLibsql)
        {
            services.AddHostedService<ManagedLibsqlPrimaryHostedService>();
        }

        services.AddHostedService<LibsqlDatabaseInitializer>();
        AddPersistentRepositories(services);
    }

    private static void AddSqliteProvider(
        IServiceCollection services,
        IConfiguration configuration)
    {
        var dataSource = configuration["Storage:Sqlite:DataSource"] ?? "/var/lib/nona/nona.db";
        var timeoutSeconds = ConfigurationValueReader.GetInt32(
            configuration,
            "Storage:Sqlite:TimeoutSeconds",
            30);

        services.AddOptions<SqliteOptions>()
            .Configure(options =>
            {
                options.DataSource = dataSource;
                options.TimeoutSeconds = timeoutSeconds;
            })
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.DataSource),
                "Storage:Sqlite:DataSource must be configured.")
            .Validate(
                options => options.TimeoutSeconds > 0,
                "Storage:Sqlite:TimeoutSeconds must be greater than zero.")
            .ValidateOnStart();

        services.AddSingleton<SqliteDatabaseClient>();
        services.AddSingleton<ILibsqlDatabaseClient>(
            serviceProvider => serviceProvider.GetRequiredService<SqliteDatabaseClient>());
        services.AddHostedService<LibsqlDatabaseInitializer>();

        AddPersistentRepositories(services);
    }

    private static void AddPersistentRepositories(IServiceCollection services)
    {
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
}
