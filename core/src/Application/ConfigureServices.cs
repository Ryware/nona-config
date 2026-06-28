global using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mediator;
using Nona.Application.Common.Interfaces;

namespace Nona.Application;

public static class ConfigureServices
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMediator(options =>
        {
            options.ServiceLifetime = ServiceLifetime.Scoped;
            options.Assemblies = [typeof(ConfigureServices).Assembly];
        });

        services.AddScoped<IProjectAccessService, ProjectAccessService>();
        services.AddScoped<IUserAuthorizationService, UserAuthorizationService>();

        return services;
    }
}
