global using Nona.Application.Common.Interfaces;
global using Nona.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nona.Domain.Interfaces;
using Nona.Infrastructure.Repositories.InMemory;
using Nona.Infrastructure.Repositories.Sqlite;

namespace Nona.Infrastructure;

public static class ConfigureServices
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IGuidGenerator, GuidGeneratorService>();
        services.AddSingleton<IDateTime, DateTimeService>();
        services.AddSingleton<IRandom, RandomService>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();

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
        else
        {
            ConfigureInMemoryPersistence(services);
        }
    }

    private static void ConfigureInMemoryPersistence(IServiceCollection services)
    {
        // In-memory repositories
        services.AddSingleton<IUserRepository, InMemoryUserRepository>();
        services.AddSingleton<IProjectRepository, InMemoryProjectRepository>();
        services.AddSingleton<IEnvironmentRepository, InMemoryEnvironmentRepository>();
        services.AddSingleton<IConfigEntryRepository, InMemoryConfigEntryRepository>();
        services.AddSingleton<IProjectMemberRepository, InMemoryProjectMemberRepository>();
    }

    private static void ConfigureSqlitePersistence(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Sqlite") 
            ?? "Data Source=nona-config.db";

        // Register the database context as a singleton
        services.AddSingleton(sp => new SqliteDbContext(connectionString));

        // Initialize database on startup
        services.AddHostedService<SqliteDatabaseInitializer>();

        // SQLite repositories
        services.AddSingleton<IConfigEntryRepository, SqliteConfigEntryRepository>();
        services.AddSingleton<IUserRepository, SqliteUserRepository>();
        services.AddSingleton<IProjectRepository, SqliteProjectRepository>();
        services.AddSingleton<IEnvironmentRepository, SqliteEnvironmentRepository>();
        services.AddSingleton<IProjectMemberRepository, SqliteProjectMemberRepository>();
    }
}
