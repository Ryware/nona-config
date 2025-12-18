global using Nona.Application.Common.Interfaces;
global using Nona.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nona.Domain.Interfaces;
using Nona.Infrastructure.Repositories;
using Nona.Infrastructure.Seeding;

namespace Nona.Infrastructure;

public static class ConfigureServices
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IGuidGenerator, GuidGeneratorService>();
        services.AddSingleton<IDateTime, DateTimeService>();
        services.AddSingleton<IRandom, RandomService>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();

        ConfigurePersistence(services);

        // Seeding
        services.AddSingleton<DataSeeder>();

        return services;
    }

    private static void ConfigurePersistence(IServiceCollection services)
    {
        // In-memory repositories
        services.AddSingleton<IUserRepository, InMemoryUserRepository>();
        services.AddSingleton<IProjectRepository, InMemoryProjectRepository>();
        services.AddSingleton<IEnvironmentRepository, InMemoryEnvironmentRepository>();
        services.AddSingleton<IConfigEntryRepository, InMemoryConfigEntryRepository>();
        services.AddSingleton<IProjectMemberRepository, InMemoryProjectMemberRepository>();
    }
}
