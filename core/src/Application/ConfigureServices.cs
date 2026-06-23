global using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MediatR;
using Nona.Application.Admin.ApiKeys.Commands;
using Nona.Application.Admin.ApiKeys.Queries;
using Nona.Application.Admin.AuditLogs.Queries;
using Nona.Application.Admin.ConfigEntries.Commands;
using Nona.Application.Admin.ConfigEntries.Queries;
using Nona.Application.Admin.Dashboard.Queries;
using Nona.Application.Admin.Environments.Commands;
using Nona.Application.Admin.Environments.Queries;
using Nona.Application.Admin.Projects.Commands;
using Nona.Application.Admin.Projects.Queries;
using Nona.Application.Admin.Users.Commands;
using Nona.Application.Admin.Users.Queries;
using Nona.Application.Api.ConfigEntries.Queries;
using Nona.Application.Auth.Commands;
using Nona.Application.Auth.Queries;
using Nona.Application.Common.Interfaces;

namespace Nona.Application;

public static class ConfigureServices
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IMediator, ApplicationMediator>();

        services.AddScoped<CreateApiKeyCommandHandler>();
        services.AddScoped<DeleteApiKeyCommandHandler>();
        services.AddScoped<ListApiKeysQueryHandler>();
        services.AddScoped<ListAuditLogsQueryHandler>();
        services.AddScoped<DeleteConfigEntryCommandHandler>();
        services.AddScoped<RollbackConfigEntryCommandHandler>();
        services.AddScoped<UpsertConfigEntryCommandHandler>();
        services.AddScoped<GetConfigEntriesQueryHandler>();
        services.AddScoped<GetConfigEntryQueryHandler>();
        services.AddScoped<ListConfigEntryVersionsQueryHandler>();
        services.AddScoped<GetDashboardCountQueryHandler>();
        services.AddScoped<CreateEnvironmentCommandHandler>();
        services.AddScoped<DeleteEnvironmentCommandHandler>();
        services.AddScoped<ListEnvironmentsQueryHandler>();
        services.AddScoped<CreateProjectCommandHandler>();
        services.AddScoped<DeleteProjectCommandHandler>();
        services.AddScoped<ListProjectsQueryHandler>();
        services.AddScoped<CreateUserCommandHandler>();
        services.AddScoped<DeleteUserCommandHandler>();
        services.AddScoped<RemoveProjectAccessCommandHandler>();
        services.AddScoped<SetProjectAccessCommandHandler>();
        services.AddScoped<UpdateUserCommandHandler>();
        services.AddScoped<AnyUsersQueryHandler>();
        services.AddScoped<GetUserProjectsQueryHandler>();
        services.AddScoped<GetUserQueryHandler>();
        services.AddScoped<ListUsersQueryHandler>();
        services.AddScoped<GetConfigEntryValueQueryHandler>();
        services.AddScoped<CompleteInvitationWithPasswordCommandHandler>();
        services.AddScoped<CompleteInvitationWithSsoCommandHandler>();
        services.AddScoped<LoginCommandHandler>();
        services.AddScoped<LoginWithSsoCommandHandler>();
        services.AddScoped<RegisterCommandHandler>();
        services.AddScoped<RequestPasswordResetCommandHandler>();
        services.AddScoped<GetInvitationQueryHandler>();

        services.AddScoped<IProjectAccessService, ProjectAccessService>();
        services.AddScoped<IUserAuthorizationService, UserAuthorizationService>();

        return services;
    }
}
