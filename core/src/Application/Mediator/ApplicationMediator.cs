using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Nona.Application.Admin.ApiKeys.Commands;
using Nona.Application.Admin.ApiKeys.Queries;
using Nona.Application.Admin.AuditLogs.DTOs;
using Nona.Application.Admin.AuditLogs.Queries;
using Nona.Application.Admin.ConfigEntries.Commands;
using Nona.Application.Admin.ConfigEntries.Queries;
using Nona.Application.Admin.Dashboard.DTOs;
using Nona.Application.Admin.Dashboard.Queries;
using Nona.Application.Admin.Environments.Commands;
using Nona.Application.Admin.Environments.Queries;
using Nona.Application.Admin.Projects.DTOs;
using Nona.Application.Admin.Projects.Commands;
using Nona.Application.Admin.Projects.Queries;
using Nona.Application.Admin.Users.Commands;
using Nona.Application.Admin.Users.DTOs;
using Nona.Application.Admin.Users.Queries;
using Nona.Application.Api.ConfigEntries.Queries;
using Nona.Application.Auth.Commands;
using Nona.Application.Auth.Queries;

namespace Nona.Application;

public sealed class ApplicationMediator(IServiceProvider services) : IMediator
{
    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        return request switch
        {
            CreateApiKeyCommand command => Cast<TResponse, CreateApiKeyResult>(
                services.GetRequiredService<CreateApiKeyCommandHandler>().Handle(command, cancellationToken)),
            DeleteApiKeyCommand command => Cast<TResponse, DeleteApiKeyResult>(
                services.GetRequiredService<DeleteApiKeyCommandHandler>().Handle(command, cancellationToken)),
            ListApiKeysQuery query => Cast<TResponse, ListApiKeysResult>(
                services.GetRequiredService<ListApiKeysQueryHandler>().Handle(query, cancellationToken)),
            ListAuditLogsQuery query => Cast<TResponse, IReadOnlyList<AuditLogDto>>(
                services.GetRequiredService<ListAuditLogsQueryHandler>().Handle(query, cancellationToken)),
            DeleteConfigEntryCommand command => Cast<TResponse, DeleteConfigEntryResult>(
                services.GetRequiredService<DeleteConfigEntryCommandHandler>().Handle(command, cancellationToken)),
            RollbackConfigEntryCommand command => Cast<TResponse, RollbackConfigEntryResult>(
                services.GetRequiredService<RollbackConfigEntryCommandHandler>().Handle(command, cancellationToken)),
            UpsertConfigEntryCommand command => Cast<TResponse, UpsertConfigEntryResult>(
                services.GetRequiredService<UpsertConfigEntryCommandHandler>().Handle(command, cancellationToken)),
            GetConfigEntriesQuery query => Cast<TResponse, GetConfigEntriesResult>(
                services.GetRequiredService<GetConfigEntriesQueryHandler>().Handle(query, cancellationToken)),
            GetConfigEntryQuery query => Cast<TResponse, GetConfigEntryResult>(
                services.GetRequiredService<GetConfigEntryQueryHandler>().Handle(query, cancellationToken)),
            ListConfigEntryVersionsQuery query => Cast<TResponse, ListConfigEntryVersionsResult>(
                services.GetRequiredService<ListConfigEntryVersionsQueryHandler>().Handle(query, cancellationToken)),
            GetDashboardCountsQuery query => Cast<TResponse, DashboardCountDto>(
                services.GetRequiredService<GetDashboardCountQueryHandler>().Handle(query, cancellationToken)),
            CreateEnvironmentCommand command => Cast<TResponse, CreateEnvironmentResult>(
                services.GetRequiredService<CreateEnvironmentCommandHandler>().Handle(command, cancellationToken)),
            DeleteEnvironmentCommand command => Cast<TResponse, DeleteEnvironmentResult>(
                services.GetRequiredService<DeleteEnvironmentCommandHandler>().Handle(command, cancellationToken)),
            ListEnvironmentsQuery query => Cast<TResponse, ListEnvironmentsResult>(
                services.GetRequiredService<ListEnvironmentsQueryHandler>().Handle(query, cancellationToken)),
            CreateProjectCommand command => Cast<TResponse, CreateProjectResult>(
                services.GetRequiredService<CreateProjectCommandHandler>().Handle(command, cancellationToken)),
            DeleteProjectCommand command => Cast<TResponse, DeleteProjectResult>(
                services.GetRequiredService<DeleteProjectCommandHandler>().Handle(command, cancellationToken)),
            ListProjectsQuery query => Cast<TResponse, IReadOnlyList<ProjectDto>>(
                services.GetRequiredService<ListProjectsQueryHandler>().Handle(query, cancellationToken)),
            CreateUserCommand command => Cast<TResponse, CreateUserResult>(
                services.GetRequiredService<CreateUserCommandHandler>().Handle(command, cancellationToken)),
            DeleteUserCommand command => Cast<TResponse, DeleteUserResult>(
                services.GetRequiredService<DeleteUserCommandHandler>().Handle(command, cancellationToken)),
            RemoveProjectAccessCommand command => Cast<TResponse, RemoveProjectAccessResult>(
                services.GetRequiredService<RemoveProjectAccessCommandHandler>().Handle(command, cancellationToken)),
            SetProjectAccessCommand command => Cast<TResponse, SetProjectAccessResult>(
                services.GetRequiredService<SetProjectAccessCommandHandler>().Handle(command, cancellationToken)),
            UpdateUserCommand command => Cast<TResponse, UpdateUserResult>(
                services.GetRequiredService<UpdateUserCommandHandler>().Handle(command, cancellationToken)),
            AnyUsersQuery query => Cast<TResponse, bool>(
                services.GetRequiredService<AnyUsersQueryHandler>().Handle(query, cancellationToken)),
            GetUserProjectsQuery query => Cast<TResponse, GetUserProjectsResult>(
                services.GetRequiredService<GetUserProjectsQueryHandler>().Handle(query, cancellationToken)),
            GetUserQuery query => Cast<TResponse, GetUserResult>(
                services.GetRequiredService<GetUserQueryHandler>().Handle(query, cancellationToken)),
            ListUsersQuery query => Cast<TResponse, IReadOnlyList<UserDto>>(
                services.GetRequiredService<ListUsersQueryHandler>().Handle(query, cancellationToken)),
            GetConfigEntryValueQuery query => Cast<TResponse, GetConfigEntryValueResult>(
                services.GetRequiredService<GetConfigEntryValueQueryHandler>().Handle(query, cancellationToken)),
            CompleteInvitationWithPasswordCommand command => Cast<TResponse, LoginResult>(
                services.GetRequiredService<CompleteInvitationWithPasswordCommandHandler>().Handle(command, cancellationToken)),
            CompleteInvitationWithSsoCommand command => Cast<TResponse, LoginResult>(
                services.GetRequiredService<CompleteInvitationWithSsoCommandHandler>().Handle(command, cancellationToken)),
            LoginCommand command => Cast<TResponse, LoginResult>(
                services.GetRequiredService<LoginCommandHandler>().Handle(command, cancellationToken)),
            LoginWithSsoCommand command => Cast<TResponse, LoginResult>(
                services.GetRequiredService<LoginWithSsoCommandHandler>().Handle(command, cancellationToken)),
            RegisterCommand command => Cast<TResponse, RegisterResult>(
                services.GetRequiredService<RegisterCommandHandler>().Handle(command, cancellationToken)),
            GetInvitationQuery query => Cast<TResponse, GetInvitationResult>(
                services.GetRequiredService<GetInvitationQueryHandler>().Handle(query, cancellationToken)),
            _ => throw new InvalidOperationException($"No handler is registered for request type '{request.GetType().FullName}'.")
        };
    }

    public async Task<object?> Send(object request, CancellationToken cancellationToken = default)
    {
        return request switch
        {
            IRequest<object?> typedRequest => await Send(typedRequest, cancellationToken),
            RequestPasswordResetCommand command => await HandleVoid(command, cancellationToken),
            _ => await SendObject(request, cancellationToken)
        };
    }

    public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IRequest
    {
        return request switch
        {
            RequestPasswordResetCommand command => services
                .GetRequiredService<RequestPasswordResetCommandHandler>()
                .Handle(command, cancellationToken),
            _ => throw new InvalidOperationException($"No handler is registered for request type '{request.GetType().FullName}'.")
        };
    }

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
        IStreamRequest<TResponse> request,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Streaming requests are not used by Nona.");
    }

    public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Streaming requests are not used by Nona.");
    }

    public Task Publish(object notification, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Notifications are not used by Nona.");
    }

    public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        throw new NotSupportedException("Notifications are not used by Nona.");
    }

    private static async Task<TResponse> Cast<TResponse, TActual>(Task<TActual> task)
    {
        return (TResponse)(object)(await task)!;
    }

    private async Task<object?> SendObject(object request, CancellationToken cancellationToken)
    {
        return request switch
        {
            CreateApiKeyCommand command => await Send(command, cancellationToken),
            DeleteApiKeyCommand command => await Send(command, cancellationToken),
            ListApiKeysQuery query => await Send(query, cancellationToken),
            ListAuditLogsQuery query => await Send(query, cancellationToken),
            DeleteConfigEntryCommand command => await Send(command, cancellationToken),
            RollbackConfigEntryCommand command => await Send(command, cancellationToken),
            UpsertConfigEntryCommand command => await Send(command, cancellationToken),
            GetConfigEntriesQuery query => await Send(query, cancellationToken),
            GetConfigEntryQuery query => await Send(query, cancellationToken),
            ListConfigEntryVersionsQuery query => await Send(query, cancellationToken),
            GetDashboardCountsQuery query => await Send(query, cancellationToken),
            CreateEnvironmentCommand command => await Send(command, cancellationToken),
            DeleteEnvironmentCommand command => await Send(command, cancellationToken),
            ListEnvironmentsQuery query => await Send(query, cancellationToken),
            CreateProjectCommand command => await Send(command, cancellationToken),
            DeleteProjectCommand command => await Send(command, cancellationToken),
            ListProjectsQuery query => await Send(query, cancellationToken),
            CreateUserCommand command => await Send(command, cancellationToken),
            DeleteUserCommand command => await Send(command, cancellationToken),
            RemoveProjectAccessCommand command => await Send(command, cancellationToken),
            SetProjectAccessCommand command => await Send(command, cancellationToken),
            UpdateUserCommand command => await Send(command, cancellationToken),
            AnyUsersQuery query => await Send(query, cancellationToken),
            GetUserProjectsQuery query => await Send(query, cancellationToken),
            GetUserQuery query => await Send(query, cancellationToken),
            ListUsersQuery query => await Send(query, cancellationToken),
            GetConfigEntryValueQuery query => await Send(query, cancellationToken),
            CompleteInvitationWithPasswordCommand command => await Send(command, cancellationToken),
            CompleteInvitationWithSsoCommand command => await Send(command, cancellationToken),
            LoginCommand command => await Send(command, cancellationToken),
            LoginWithSsoCommand command => await Send(command, cancellationToken),
            RegisterCommand command => await Send(command, cancellationToken),
            GetInvitationQuery query => await Send(query, cancellationToken),
            _ => throw new InvalidOperationException($"No handler is registered for request type '{request.GetType().FullName}'.")
        };
    }

    private async Task<object?> HandleVoid(RequestPasswordResetCommand command, CancellationToken cancellationToken)
    {
        await services.GetRequiredService<RequestPasswordResetCommandHandler>()
            .Handle(command, cancellationToken);
        return null;
    }
}
