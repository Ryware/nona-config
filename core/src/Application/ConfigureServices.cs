global using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mediator;
using Nona.Application.Admin.ApiKeys.Commands;
using Nona.Application.Admin.ApiKeys.Validators;
using Nona.Application.Admin.ConfigReleases.Commands;
using Nona.Application.Admin.ConfigReleases.Validators;
using Nona.Application.Admin.ConfigEntries.Commands;
using Nona.Application.Admin.ConfigEntries.Validators;
using Nona.Application.Admin.Environments.Commands;
using Nona.Application.Admin.Environments.Validators;
using Nona.Application.Admin.Projects.Commands;
using Nona.Application.Admin.Projects.Validators;
using Nona.Application.Admin.Users.Commands;
using Nona.Application.Admin.Users.Validators;
using Nona.Application.Auth.Commands;
using Nona.Application.Auth.DTOs;
using Nona.Application.Auth.Validators;
using Nona.Application.Common.Behaviors;
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
            options.PipelineBehaviors = [typeof(ValidationPipelineBehavior<,>)];
        });

        services.AddScoped<IValidator<CreateApiKeyRequest>, CreateApiKeyRequestValidator>();
        services.AddScoped<IValidator<PublishConfigReleaseRequest>, PublishConfigReleaseRequestValidator>();
        services.AddScoped<IValidator<SetActiveConfigReleaseRequest>, SetActiveConfigReleaseRequestValidator>();
        services.AddScoped<IValidator<UpsertConfigEntryRequest>, UpsertConfigEntryRequestValidator>();
        services.AddScoped<IValidator<CreateEnvironmentRequest>, CreateEnvironmentRequestValidator>();
        services.AddScoped<IValidator<CreateProjectRequest>, CreateProjectRequestValidator>();
        services.AddScoped<IValidator<CreateUserRequest>, CreateUserRequestValidator>();
        services.AddScoped<IValidator<UpdateUserRequest>, UpdateUserRequestValidator>();
        services.AddScoped<IValidator<ProjectAccessRequest>, ProjectAccessRequestValidator>();
        services.AddScoped<IValidator<CompleteInvitationPasswordRequest>, CompleteInvitationPasswordRequestValidator>();
        services.AddScoped<IValidator<LoginRequest>, LoginRequestValidator>();
        services.AddScoped<IValidator<RegisterCommand>, RegisterCommandValidator>();
        services.AddScoped<IValidator<RequestPasswordResetCommand>, RequestPasswordResetCommandValidator>();

        services.AddScoped<IProjectAccessService, ProjectAccessService>();
        services.AddScoped<IUserAuthorizationService, UserAuthorizationService>();

        return services;
    }
}
