using Mediator;
using Microsoft.AspNetCore.Http.HttpResults;
using Nona.Application.Admin.ApiKeys.Commands;
using Nona.Application.Admin.ApiKeys.DTOs;
using Nona.Application.Admin.ApiKeys.Queries;
using Nona.Application.Admin.AuditLogs.DTOs;
using Nona.Application.Admin.Common;
using Nona.Application.Admin.ConfigEntries.Commands;
using Nona.Application.Admin.ConfigEntries.DTOs;
using Nona.Application.Admin.ConfigEntries.Queries;
using Nona.Application.Admin.Dashboard.DTOs;
using Nona.Application.Admin.Dashboard.Queries;
using Nona.Application.Admin.Environments.Commands;
using Nona.Application.Admin.Environments.DTOs;
using Nona.Application.Admin.Environments.Queries;
using Nona.Application.Admin.Projects.Commands;
using Nona.Application.Admin.Projects.DTOs;
using Nona.Application.Admin.Projects.Queries;
using Nona.Application.Admin.Users.Commands;
using Nona.Application.Admin.Users.DTOs;
using Nona.Application.Admin.Users.Queries;
using Nona.Application.Api.ConfigEntries.Queries;
using Nona.Application.Auth;
using Nona.Application.Auth.Commands;
using Nona.Application.Auth.DTOs;
using Nona.Application.Auth.Queries;
using Nona.Application.Common;
using Nona.Application.Common.Interfaces;
using Nona.WebApi.Authentication;
using Nona.WebApi.Serialization;

namespace Nona.WebApi.Endpoints;

public static class NonaEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapNonaEndpoints(this IEndpointRouteBuilder app)
    {
        MapAuthEndpoints(app.MapGroup("/auth"));
        MapAdminEndpoints(app.MapGroup("/admin").RequireAuthorization());
        MapConfigApiEndpoints(app.MapGroup("/api"));

        return app;
    }

    private static void MapAuthEndpoints(RouteGroupBuilder auth)
    {
        auth.MapPost("/login", LoginAsync)
            .Produces<LoginResponse>();
        auth.MapGet("/sso/config", GetSsoConfiguration)
            .Produces<SsoPublicConfigResponse>();
        auth.MapPost("/sso/google", LoginWithGoogleAsync)
            .Produces<LoginResponse>();
        auth.MapPost("/sso/microsoft", LoginWithMicrosoftAsync)
            .Produces<LoginResponse>();
        auth.MapGet("/first-time", CheckIfAnyUsersExistAsync)
            .Produces<bool>();
        auth.MapPost("/register", RegisterAsync)
            .Produces<RegisterResult>();
        auth.MapPost("/forgot-password", RequestPasswordResetAsync)
            .Produces(StatusCodes.Status204NoContent);
        auth.MapGet("/invitations/{token}", GetInvitationAsync)
            .Produces<InvitationDetailsResponse>();
        auth.MapPost("/invitations/{token}/password", CompleteInvitationWithPasswordAsync)
            .Produces<LoginResponse>();
        auth.MapPost("/invitations/{token}/sso/{provider}", CompleteInvitationWithSsoAsync)
            .Produces<LoginResponse>();
    }

    private static void MapAdminEndpoints(RouteGroupBuilder admin)
    {
        var projects = admin.MapGroup("/projects");
        projects.MapPost("/", CreateProjectAsync)
            .Produces<ProjectDto>(StatusCodes.Status201Created);
        projects.MapGet("/", ListProjectsAsync)
            .Produces<IReadOnlyList<ProjectDto>>();
        projects.MapDelete("/{projectId}", DeleteProjectAsync);

        var environments = projects.MapGroup("/{projectId}/environments");
        environments.MapPost("/", CreateEnvironmentAsync)
            .Produces<EnvironmentDto>(StatusCodes.Status201Created);
        environments.MapGet("/", ListEnvironmentsAsync)
            .Produces<IReadOnlyList<EnvironmentDto>>();
        environments.MapDelete("/{environmentId}", DeleteEnvironmentAsync);

        var apiKeys = projects.MapGroup("/{projectId}/api-keys");
        apiKeys.MapGet("/", ListApiKeysAsync)
            .Produces<IReadOnlyList<ApiKeyDto>>();
        apiKeys.MapPost("/", CreateApiKeyAsync)
            .Produces<ApiKeyDto>(StatusCodes.Status201Created);
        apiKeys.MapDelete("/{apiKeyId}", DeleteApiKeyAsync);

        var configEntries = projects.MapGroup("/{projectId}/environments/{environmentName}/config-entries");
        configEntries.MapGet("/", GetConfigEntriesAsync)
            .Produces<IReadOnlyList<ConfigEntryDto>>();
        configEntries.MapGet("/{key}", GetConfigEntryAsync)
            .Produces<ConfigEntryDto>();
        configEntries.MapPut("/{key}", UpsertConfigEntryAsync)
            .Accepts<UpsertConfigEntryRequest>("application/json")
            .Produces<ConfigEntryDto>();
        configEntries.MapDelete("/{key}", DeleteConfigEntryAsync);

        configEntries.MapGet(
                "/{key}/history",
                async (string projectId, string environmentName, string key, IMediator mediator, CancellationToken cancellationToken) =>
                    await GetConfigEntryHistoryAsync(projectId, environmentName, key, mediator, cancellationToken))
            .Produces<IReadOnlyList<ConfigEntryVersionDto>>();
        configEntries.MapPost(
                "/{key}/rollback",
                async (string projectId, string environmentName, string key, RollbackConfigEntryRequest request, IMediator mediator, CancellationToken cancellationToken) =>
                    await RollbackConfigEntryAsync(projectId, environmentName, key, request, mediator, cancellationToken))
            .Accepts<RollbackConfigEntryRequest>("application/json")
            .Produces<ConfigEntryDto>();

        var users = admin.MapGroup("/users");
        users.MapPost("/", CreateUserAsync)
            .Produces<CreateUserResponse>(StatusCodes.Status201Created);
        users.MapGet("/", ListUsersAsync)
            .Produces<IReadOnlyList<UserDto>>();
        users.MapGet("/{id}", GetUserAsync)
            .Produces<UserDto>();
        users.MapPut("/{id}", UpdateUserAsync)
            .Produces<UserDto>();
        users.MapDelete("/{id}", DeleteUserAsync);
        users.MapGet("/{id}/projects", GetUserProjectsAsync)
            .Produces<IReadOnlyList<ProjectAccessDto>>();
        users.MapPut("/{id}/projects/{projectName}", SetProjectAccessAsync)
            .Produces<ProjectAccessDto>();
        users.MapDelete("/{id}/projects/{projectName}", RemoveProjectAccessAsync);

        admin.MapGet("/audit-logs", ListAuditLogsAsync)
            .Produces<IReadOnlyList<AuditLogDto>>();
        admin.MapGet("/dashboard/counts", GetDashboardCountsAsync)
            .Produces<DashboardCountDto>();
    }

    private static void MapConfigApiEndpoints(RouteGroupBuilder api)
    {
        api.MapGet("/{environmentId}/{key}", GetConfigValueAsync)
            .RequireAuthorization(ApiKeyAuthenticationHandler.SchemeName);
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new LoginCommand(request.Email, request.Password), cancellationToken);
        return result.Success
            ? Results.Ok(result.Response)
            : Unauthorized(result.Error ?? "Invalid username or password");
    }

    private static IResult GetSsoConfiguration(ISsoPublicConfigurationProvider provider)
    {
        return Results.Ok(provider.GetConfiguration());
    }

    private static Task<IResult> LoginWithGoogleAsync(
        SsoLoginRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        return LoginWithSsoAsync(SsoProviders.Google, request, mediator, cancellationToken);
    }

    private static Task<IResult> LoginWithMicrosoftAsync(
        SsoLoginRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        return LoginWithSsoAsync(SsoProviders.Microsoft, request, mediator, cancellationToken);
    }

    private static async Task<IResult> CheckIfAnyUsersExistAsync(IMediator mediator, CancellationToken cancellationToken)
    {
        return Results.Ok(await mediator.Send(new AnyUsersQuery(), cancellationToken));
    }

    private static async Task<IResult> RegisterAsync(
        RegisterCommand command,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        return Results.Ok(await mediator.Send(command, cancellationToken));
    }

    private static async Task<IResult> RequestPasswordResetAsync(
        RequestPasswordResetCommand command,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        await mediator.Send(command, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> GetInvitationAsync(
        string token,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetInvitationQuery(token), cancellationToken);
        return result.Success
            ? Results.Ok(result.Invitation)
            : NotFound(result.Error ?? "Invitation not found", result.ErrorCode);
    }

    private static async Task<IResult> CompleteInvitationWithPasswordAsync(
        string token,
        CompleteInvitationPasswordRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new CompleteInvitationWithPasswordCommand(token, request.NewPassword),
            cancellationToken);

        if (result.Success)
        {
            return Results.Ok(result.Response);
        }

        return result.ErrorCode == AuthErrorCodes.InvitationInvalidOrUsed
            ? NotFound(result.Error ?? "Invitation not found", result.ErrorCode)
            : BadRequest(result.Error ?? "Invitation could not be completed", result.ErrorCode);
    }

    private static async Task<IResult> CompleteInvitationWithSsoAsync(
        string token,
        string provider,
        SsoLoginRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new CompleteInvitationWithSsoCommand(token, provider, request.IdToken),
            cancellationToken);

        if (result.Success)
        {
            return Results.Ok(result.Response);
        }

        return result.ErrorCode == AuthErrorCodes.InvitationInvalidOrUsed
            ? NotFound(result.Error ?? "Invitation not found", result.ErrorCode)
            : Unauthorized(result.Error ?? "Authentication failed", result.ErrorCode);
    }

    private static async Task<IResult> LoginWithSsoAsync(
        string provider,
        SsoLoginRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new LoginWithSsoCommand(provider, request.IdToken), cancellationToken);
        return result.Success
            ? Results.Ok(result.Response)
            : Unauthorized(result.Error ?? "Authentication failed", result.ErrorCode);
    }

    private static async Task<IResult> CreateProjectAsync(
        CreateProjectRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new CreateProjectCommand(request.Name), cancellationToken);
        if (result.Success)
        {
            return Results.Created("/admin/projects", result.Project);
        }

        return result.Error == "Project already exists"
            ? Conflict(result.Error)
            : BadRequest(result.Error ?? "Project could not be created");
    }

    private static async Task<IResult> ListProjectsAsync(IMediator mediator, CancellationToken cancellationToken)
    {
        return Results.Ok(await mediator.Send(new ListProjectsQuery(), cancellationToken));
    }

    private static async Task<IResult> DeleteProjectAsync(
        string projectId,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new DeleteProjectCommand(projectId), cancellationToken);
        return result.Success
            ? Results.NoContent()
            : NotFound(result.Error ?? "Project not found");
    }

    private static async Task<IResult> CreateEnvironmentAsync(
        string projectId,
        CreateEnvironmentRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new CreateEnvironmentCommand(projectId, request.Name), cancellationToken);
        if (result.Success)
        {
            return Results.Created($"/admin/projects/{projectId}/environments", result.Environment);
        }

        return result.Error switch
        {
            "Project not found" => NotFound(result.Error),
            "Environment already exists" => Conflict(result.Error),
            _ => BadRequest(result.Error ?? "Environment could not be created")
        };
    }

    private static async Task<IResult> ListEnvironmentsAsync(
        string projectId,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ListEnvironmentsQuery(projectId), cancellationToken);
        return result.Success
            ? Results.Ok(result.Environments)
            : NotFound(result.Error ?? "Project not found");
    }

    private static async Task<IResult> DeleteEnvironmentAsync(
        string projectId,
        string environmentId,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new DeleteEnvironmentCommand(projectId, environmentId), cancellationToken);
        return result.Success
            ? Results.NoContent()
            : NotFound(result.Error ?? "Environment not found");
    }

    private static async Task<IResult> ListApiKeysAsync(
        string projectId,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ListApiKeysQuery(projectId), cancellationToken);
        if (result.Success)
        {
            return Results.Ok(result.ApiKeys);
        }

        return result.Error switch
        {
            "Project not found" => NotFound(result.Error),
            "Access denied" => Results.Forbid(),
            _ => BadRequest(result.Error ?? "API keys could not be listed")
        };
    }

    private static async Task<IResult> CreateApiKeyAsync(
        string projectId,
        CreateApiKeyRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new CreateApiKeyCommand(projectId, request.Name, request.Environment, request.Scope),
            cancellationToken);

        if (result.Success)
        {
            return Results.Created($"/admin/projects/{projectId}/api-keys", result.ApiKey);
        }

        return result.Error switch
        {
            "Project not found" or "Environment not found" => NotFound(result.Error),
            "Access denied" => Results.Forbid(),
            _ => BadRequest(result.Error ?? "API key could not be created")
        };
    }

    private static async Task<IResult> DeleteApiKeyAsync(
        string projectId,
        long apiKeyId,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new DeleteApiKeyCommand(projectId, apiKeyId), cancellationToken);
        return result.Success
            ? Results.NoContent()
            : result.Error switch
            {
                "Project not found" or "API key not found" => NotFound(result.Error),
                "Access denied" => Results.Forbid(),
                _ => BadRequest(result.Error ?? "API key could not be deleted")
            };
    }

    private static async Task<IResult> GetConfigEntriesAsync(
        string projectId,
        string environmentName,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetConfigEntriesQuery(projectId, environmentName), cancellationToken);
        return result.Success
            ? Results.Ok(result.ConfigEntries)
            : NotFound(result.Error ?? "Config entries not found");
    }

    private static async Task<IResult> GetConfigEntryAsync(
        string projectId,
        string environmentName,
        string key,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetConfigEntryQuery(projectId, environmentName, key), cancellationToken);
        return result.Success
            ? Results.Ok(result.ConfigEntry)
            : NotFound(result.Error ?? "Config entry not found");
    }

    private static async Task<IResult> GetConfigEntryHistoryAsync(
        string projectId,
        string environmentName,
        string key,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ListConfigEntryVersionsQuery(projectId, environmentName, key), cancellationToken);
        return result.Success
            ? Results.Ok(result.Versions)
            : NotFound(result.Error ?? "Config entry history not found");
    }

    private static async Task<IResult> UpsertConfigEntryAsync(
        string projectId,
        string environmentName,
        string key,
        UpsertConfigEntryRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        if (!ValidationHelpers.IsValidKey(key))
        {
            return BadRequest("Key must be non-empty and contain no spaces");
        }

        var result = await mediator.Send(
            new UpsertConfigEntryCommand(projectId, environmentName, key, request.Value, request.ContentType, request.Scope),
            cancellationToken);

        if (result.Success)
        {
            return Results.Ok(result.ConfigEntry);
        }

        return result.Error switch
        {
            "Project not found" or "Environment not found" => NotFound(result.Error),
            _ => BadRequest(result.Error ?? "Config entry could not be saved")
        };
    }

    private static async Task<IResult> RollbackConfigEntryAsync(
        string projectId,
        string environmentName,
        string key,
        RollbackConfigEntryRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        if (!ValidationHelpers.IsValidKey(key))
        {
            return BadRequest("Key must be non-empty and contain no spaces");
        }

        var result = await mediator.Send(
            new RollbackConfigEntryCommand(projectId, environmentName, key, request.Version),
            cancellationToken);

        if (result.Success)
        {
            return Results.Ok(result.ConfigEntry);
        }

        return result.Error switch
        {
            "Project not found" or "Environment not found" or "Config entry not found" or "Version not found" => NotFound(result.Error),
            _ => BadRequest(result.Error ?? "Config entry could not be rolled back")
        };
    }

    private static async Task<IResult> DeleteConfigEntryAsync(
        string projectId,
        string environmentName,
        string key,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new DeleteConfigEntryCommand(projectId, environmentName, key), cancellationToken);
        return result.Success
            ? Results.NoContent()
            : NotFound(result.Error ?? "Config entry not found");
    }

    private static async Task<IResult> CreateUserAsync(
        CreateUserRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new CreateUserCommand(request.Name, request.Email, request.Role, request.Scope),
            cancellationToken);

        if (result.Success)
        {
            return Results.Created($"/admin/users/{result.Response!.User.Id}", result.Response);
        }

        return result.Error == "User already exists"
            ? Conflict(result.Error)
            : BadRequest(result.Error ?? "User could not be created");
    }

    private static async Task<IResult> ListUsersAsync(IMediator mediator, CancellationToken cancellationToken)
    {
        return Results.Ok(await mediator.Send(new ListUsersQuery(), cancellationToken));
    }

    private static async Task<IResult> GetUserAsync(long id, IMediator mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetUserQuery(id), cancellationToken);
        return result.Success
            ? Results.Ok(result.User)
            : NotFound(result.Error ?? "User not found");
    }

    private static async Task<IResult> UpdateUserAsync(
        long id,
        UpdateUserRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new UpdateUserCommand(id, request.Name, request.Role, request.Scope), cancellationToken);
        if (result.Success)
        {
            return Results.Ok(result.User);
        }

        return result.Error == "User not found"
            ? NotFound(result.Error)
            : BadRequest(result.Error ?? "User could not be updated");
    }

    private static async Task<IResult> DeleteUserAsync(long id, IMediator mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new DeleteUserCommand(id), cancellationToken);
        if (result.Success)
        {
            return Results.NoContent();
        }

        return result.Error == "User not found"
            ? NotFound(result.Error)
            : BadRequest(result.Error ?? "User could not be deleted");
    }

    private static async Task<IResult> GetUserProjectsAsync(long id, IMediator mediator, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetUserProjectsQuery(id), cancellationToken);
        return result.Success
            ? Results.Ok(result.Projects)
            : NotFound(result.Error ?? "User not found");
    }

    private static async Task<IResult> SetProjectAccessAsync(
        long id,
        string projectName,
        ProjectAccessRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new SetProjectAccessCommand(id, projectName, request.Role), cancellationToken);
        if (result.Success)
        {
            return Results.Ok(result.ProjectAccess);
        }

        return result.Error switch
        {
            "User not found" or "Project not found" => NotFound(result.Error),
            _ => BadRequest(result.Error ?? "Project access could not be updated")
        };
    }

    private static async Task<IResult> RemoveProjectAccessAsync(
        long id,
        string projectName,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new RemoveProjectAccessCommand(id, projectName), cancellationToken);
        return result.Success
            ? Results.NoContent()
            : NotFound(result.Error ?? "Project access not found");
    }

    private static async Task<IResult> ListAuditLogsAsync(IMediator mediator, CancellationToken cancellationToken)
    {
        return Results.Ok(await mediator.Send(new Nona.Application.Admin.AuditLogs.Queries.ListAuditLogsQuery(), cancellationToken));
    }

    private static async Task<IResult> GetDashboardCountsAsync(IMediator mediator, CancellationToken cancellationToken)
    {
        return Results.Ok(await mediator.Send(new GetDashboardCountsQuery(), cancellationToken));
    }

    public static async Task<IResult> GetConfigValueAsync(
        string environmentId,
        string key,
        HttpContext httpContext,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetConfigEntryValueQuery(environmentId, key), cancellationToken);
        if (!result.Success)
        {
            return result.Error switch
            {
                "API key is required" or "Invalid API key" => Unauthorized(result.Error),
                _ => NotFound(result.Error ?? "Config value not found")
            };
        }

        httpContext.Response.Headers[NonaResponseHeaders.LogicalContentType] =
            result.LogicalContentType ?? ConfigEntryContentTypes.Text;

        return Results.Content(result.Value!, "application/json");
    }

    private static IResult BadRequest(string error, string? errorCode = null)
    {
        return Results.BadRequest(new ErrorResponse(error, errorCode));
    }

    private static IResult Conflict(string error)
    {
        return Results.Conflict(new ErrorResponse(error));
    }

    private static IResult NotFound(string error, string? errorCode = null)
    {
        return Results.NotFound(new ErrorResponse(error, errorCode));
    }

    private static IResult Unauthorized(string error, string? errorCode = null)
    {
        return Results.Json(
            new ErrorResponse(error, errorCode),
            NonaJsonSerializerContext.Default.ErrorResponse,
            statusCode: StatusCodes.Status401Unauthorized);
    }
}
