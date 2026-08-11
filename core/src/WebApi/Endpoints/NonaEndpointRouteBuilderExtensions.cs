using FluentValidation;
using Mediator;
using Microsoft.AspNetCore.Http.HttpResults;
using System.Security.Cryptography;
using System.Text;
using Nona.Application.Admin.ApiKeys.Commands;
using Nona.Application.Admin.ApiKeys.DTOs;
using Nona.Application.Admin.ApiKeys.Queries;
using Nona.Application.Admin.AuditLogs.DTOs;
using Nona.Application.Admin.ConfigReleases.Commands;
using Nona.Application.Admin.ConfigReleases.DTOs;
using Nona.Application.Admin.ConfigReleases.Queries;
using Nona.Application.Admin.Common;
using Nona.Application.Admin.ConfigEntries.Commands;
using Nona.Application.Admin.ConfigEntries.DTOs;
using Nona.Application.Admin.ConfigEntries.Queries;
using Nona.Application.Admin.Dashboard.DTOs;
using Nona.Application.Admin.Dashboard.Queries;
using Nona.Application.Admin.Environments.Commands;
using Nona.Application.Admin.Environments.DTOs;
using Nona.Application.Admin.Environments.Queries;
using Nona.Application.Admin.ParameterShareLinks.Commands;
using Nona.Application.Admin.ParameterShareLinks.DTOs;
using Nona.Application.Admin.ParameterShareLinks.Queries;
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
using Nona.Application.Shared.ParameterShareLinks;
using Nona.Application.Shared.ParameterShareLinks.Commands;
using Nona.Application.Shared.ParameterShareLinks.DTOs;
using Nona.Application.Shared.ParameterShareLinks.Queries;
using Nona.Domain;
using Nona.WebApi.Authentication;
using Nona.WebApi.Authorization;

namespace Nona.WebApi.Endpoints;

public static class NonaEndpointRouteBuilderExtensions
{
    private const string GoogleRedirectCookiePrefix = "nona.google.sso.";
    private const string GoogleRedirectFrontendPath = "/sso/callback/google";
    private static readonly TimeSpan GoogleRedirectCookieLifetime = TimeSpan.FromMinutes(5);

    public static IEndpointRouteBuilder MapNonaEndpoints(this IEndpointRouteBuilder app)
    {
        MapAuthEndpoints(app.MapGroup("/auth"));
        MapAdminEndpoints(app.MapGroup("/admin").RequireAuthorization());
        MapConfigApiEndpoints(app.MapGroup("/api"));
        MapSharedParameterEndpoints(app.MapGroup("/public/share-links"));

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
        auth.MapPost("/sso/google/callback", HandleGoogleRedirectCallbackAsync)
            .ExcludeFromDescription();
        auth.MapGet("/sso/google/credential", GetGoogleRedirectCredential)
            .Produces<SsoRedirectCredentialResponse>()
            .ExcludeFromDescription();
        auth.MapPost("/sso/microsoft", LoginWithMicrosoftAsync)
            .Produces<LoginResponse>();
        auth.MapGet("/first-time", CheckIfAnyUsersExistAsync)
            .Produces<bool>();
        auth.MapPost("/register", RegisterAsync)
            .Produces<LoginResponse>()
            .Produces<ApiValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces<ApiProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")
            .Produces<ApiProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json");
        auth.MapGet("/invitations/{token}", GetInvitationAsync)
            .Produces<InvitationDetailsResponse>();
        auth.MapPost("/invitations/{token}/password", CompleteInvitationWithPasswordAsync)
            .Produces<LoginResponse>();
        auth.MapPost("/invitations/{token}/sso/{provider}", CompleteInvitationWithSsoAsync)
            .Produces<LoginResponse>();
        auth.MapGet("/password-resets/{token}", GetPasswordResetAsync)
            .Produces<PasswordResetDetailsResponse>()
            .Produces<ApiProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json");
        auth.MapPost("/password-resets/{token}/password", CompletePasswordResetAsync)
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ApiValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces<ApiProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json");
        auth.MapGet("/me", GetCurrentAccountAsync)
            .Produces<AccountDetailsResponse>()
            .Produces<ApiProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")
            .RequireAuthorization();
        auth.MapPut("/password", ChangePasswordAsync)
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ApiValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces<ApiProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")
            .Produces<ApiProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")
            .RequireAuthorization();
    }

    private static void MapAdminEndpoints(RouteGroupBuilder admin)
    {
        var projects = admin.MapGroup("/projects");
        projects.MapPost("/", CreateProjectAsync)
            .Produces<ProjectDto>(StatusCodes.Status201Created);
        projects.MapGet("/", ListProjectsAsync)
            .Produces<IReadOnlyList<ProjectDto>>();
        projects.MapPut("/{projectId}", RenameProjectAsync)
            .Accepts<RenameProjectRequest>("application/json")
            .Produces<ProjectDto>();
        projects.MapDelete("/{projectId}", DeleteProjectAsync);

        var environments = projects.MapGroup("/{projectId}/environments");
        environments.MapPost("/", CreateEnvironmentAsync)
            .Produces<EnvironmentDto>(StatusCodes.Status201Created);
        environments.MapGet("/", ListEnvironmentsAsync)
            .Produces<IReadOnlyList<EnvironmentDto>>();
        environments.MapPut("/{environmentId}", RenameEnvironmentAsync)
            .Accepts<RenameEnvironmentRequest>("application/json")
            .Produces<EnvironmentDto>();
        environments.MapDelete("/{environmentId}", DeleteEnvironmentAsync);

        var configReleases = projects.MapGroup("/{projectId}/environments/{environmentName}/releases");
        configReleases.MapGet("/", ListConfigReleasesAsync)
            .Produces<IReadOnlyList<ConfigReleaseDto>>();
        configReleases.MapPost("/", PublishConfigReleaseAsync)
            .Accepts<PublishConfigReleaseRequest>("application/json")
            .Produces<ConfigReleaseDetailsDto>(StatusCodes.Status201Created);
        configReleases.MapGet("/{version}", GetConfigReleaseAsync)
            .Produces<ConfigReleaseDetailsDto>();
        configReleases.MapDelete("/{version}", DeleteConfigReleaseAsync);

        var activeRelease = projects.MapGroup("/{projectId}/environments/{environmentName}/active-release");
        activeRelease.MapPut(
                "/",
                async (string projectId, string environmentName, SetActiveConfigReleaseRequest request, IValidator<SetActiveConfigReleaseRequest> validator, IMediator mediator, CancellationToken cancellationToken) =>
                    await SetActiveConfigReleaseAsync(projectId, environmentName, request, validator, mediator, cancellationToken))
            .Accepts<SetActiveConfigReleaseRequest>("application/json")
            .Produces<EnvironmentDto>();
        activeRelease.MapDelete("/", ClearActiveConfigReleaseAsync)
            .Produces<EnvironmentDto>();

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
        configEntries.MapGet(
                "/{key}/share-links",
                async (string projectId, string environmentName, string key, IMediator mediator, CancellationToken cancellationToken) =>
                    await ListParameterShareLinksAsync(projectId, environmentName, key, mediator, cancellationToken))
            .Produces<IReadOnlyList<ParameterShareLinkDto>>();
        configEntries.MapPost(
                "/{key}/share-links",
                async (string projectId, string environmentName, string key, CreateParameterShareLinkRequest request, IMediator mediator, CancellationToken cancellationToken) =>
                    await CreateParameterShareLinkAsync(projectId, environmentName, key, request, mediator, cancellationToken))
            .Accepts<CreateParameterShareLinkRequest>("application/json")
            .Produces<CreatedParameterShareLinkDto>(StatusCodes.Status201Created);
        configEntries.MapDelete(
            "/{key}/share-links/{shareLinkId:long}",
            async (string projectId, string environmentName, string key, long shareLinkId, IMediator mediator, CancellationToken cancellationToken) =>
                await RevokeParameterShareLinkAsync(projectId, environmentName, key, shareLinkId, mediator, cancellationToken));

        var users = admin.MapGroup("/users");
        users.MapPost("/", CreateUserAsync)
            .Produces<CreateUserResponse>(StatusCodes.Status201Created);
        users.MapGet("/", ListUsersAsync)
            .Produces<IReadOnlyList<UserDto>>()
            .RequireAuthorization(AdminReadAuthorizationPolicies.Manage);
        users.MapGet("/{id}", GetUserAsync)
            .Produces<UserDto>()
            .RequireAuthorization(AdminReadAuthorizationPolicies.SelfOrManageUser);
        users.MapPut("/{id}", UpdateUserAsync)
            .Produces<UserDto>();
        users.MapDelete("/{id}", DeleteUserAsync);
        users.MapGet("/{id}/projects", GetUserProjectsAsync)
            .Produces<IReadOnlyList<ProjectAccessDto>>()
            .RequireAuthorization(AdminReadAuthorizationPolicies.SelfOrManageUser);
        users.MapPut("/{id}/projects/{projectName}", SetProjectAccessAsync)
            .Produces<ProjectAccessDto>();
        users.MapDelete("/{id}/projects/{projectName}", RemoveProjectAccessAsync);
        users.MapPost("/{id}/password-reset", GeneratePasswordResetAsync)
            .Produces<GeneratePasswordResetResponse>()
            .Produces<ApiProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")
            .Produces<ApiProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")
            .Produces<ApiProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json");

        admin.MapGet("/audit-logs/export", ExportAuditLogsAsync)
            .Produces(StatusCodes.Status200OK, contentType: "text/csv")
            .Produces(StatusCodes.Status200OK, contentType: "application/json")
            .Produces<ApiProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")
            .RequireAuthorization(AdminReadAuthorizationPolicies.Manage);
        admin.MapGet("/audit-logs", ListAuditLogsAsync)
            .Produces<AuditLogPageDto>()
            .RequireAuthorization(AdminReadAuthorizationPolicies.Manage);
        admin.MapGet("/dashboard/counts", GetDashboardCountsAsync)
            .Produces<DashboardCountDto>()
            .RequireAuthorization(AdminReadAuthorizationPolicies.Manage);
    }

    private static void MapConfigApiEndpoints(RouteGroupBuilder api)
    {
        api.MapGet("/{environmentId}", GetAllConfigValuesAsync)
            .Produces<Dictionary<string, ClientConfigValueDto>>()
            .Produces(StatusCodes.Status304NotModified)
            .Produces<ApiProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces<ApiProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")
            .Produces<ApiProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")
            .RequireAuthorization(ApiKeyAuthenticationHandler.SchemeName);

        api.MapGet("/{environmentId}/{key}", GetConfigValueAsync)
            .RequireAuthorization(ApiKeyAuthenticationHandler.SchemeName);
    }

    private static void MapSharedParameterEndpoints(RouteGroupBuilder shareLinks)
    {
        shareLinks.MapGet("/{token}", GetSharedParameterAsync)
            .Produces<SharedParameterDto>();
        shareLinks.MapPut("/{token}", UpdateSharedParameterAsync)
            .Accepts<UpdateSharedParameterRequest>("application/json")
            .Produces<SharedParameterDto>();
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        IValidator<LoginRequest> validator,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        if (await ValidateRequestAsync(request, validator, cancellationToken) is { } validationResult)
        {
            return validationResult;
        }

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

    private static async Task<IResult> HandleGoogleRedirectCallbackAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        IFormCollection form;
        try
        {
            form = await httpContext.Request.ReadFormAsync(cancellationToken);
        }
        catch (InvalidDataException)
        {
            return GoogleRedirectError(null, "missing_credential");
        }

        var flowId = form["state"].ToString();
        if (!IsValidSsoFlowId(flowId))
        {
            return GoogleRedirectError(null, "invalid_flow");
        }

        var postedCsrfToken = form["g_csrf_token"].ToString();
        var hasCsrfCookie = httpContext.Request.Cookies.TryGetValue("g_csrf_token", out var cookieCsrfToken);
        if (
            !hasCsrfCookie ||
            string.IsNullOrEmpty(postedCsrfToken) ||
            string.IsNullOrEmpty(cookieCsrfToken) ||
            !FixedTimeEquals(postedCsrfToken, cookieCsrfToken)
        )
        {
            return GoogleRedirectError(flowId, "csrf");
        }

        var credential = form["credential"].ToString();
        if (string.IsNullOrWhiteSpace(credential) || credential.Length > 3500)
        {
            return GoogleRedirectError(flowId, "missing_credential");
        }

        httpContext.Response.Cookies.Append(
            GoogleRedirectCookiePrefix + flowId,
            credential,
            CreateGoogleRedirectCookieOptions(httpContext.Request.IsHttps));

        return Results.Redirect(BuildGoogleRedirectLocation(flowId));
    }

    private static IResult GetGoogleRedirectCredential(HttpContext httpContext, string? flow)
    {
        httpContext.Response.Headers.CacheControl = "no-store";

        if (!IsValidSsoFlowId(flow))
        {
            return BadRequest("Invalid Google SSO redirect flow.");
        }

        var cookieName = GoogleRedirectCookiePrefix + flow;
        if (!httpContext.Request.Cookies.TryGetValue(cookieName, out var credential) ||
            string.IsNullOrWhiteSpace(credential))
        {
            return Unauthorized("Google SSO redirect credential is missing or expired.");
        }

        httpContext.Response.Cookies.Delete(
            cookieName,
            CreateGoogleRedirectCookieOptions(httpContext.Request.IsHttps));

        return Results.Ok(new SsoRedirectCredentialResponse(credential));
    }

    private static IResult GoogleRedirectError(string? flowId, string error)
    {
        return Results.Redirect(BuildGoogleRedirectLocation(flowId, error));
    }

    private static string BuildGoogleRedirectLocation(string? flowId, string? error = null)
    {
        var query = new List<string>();
        if (!string.IsNullOrEmpty(flowId))
        {
            query.Add($"flow={Uri.EscapeDataString(flowId)}");
        }

        if (!string.IsNullOrEmpty(error))
        {
            query.Add($"error={Uri.EscapeDataString(error)}");
        }

        return query.Count == 0
            ? GoogleRedirectFrontendPath
            : $"{GoogleRedirectFrontendPath}?{string.Join("&", query)}";
    }

    private static CookieOptions CreateGoogleRedirectCookieOptions(bool secure)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = secure,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
            MaxAge = GoogleRedirectCookieLifetime,
            Path = "/auth/sso/google"
        };
    }

    private static bool IsValidSsoFlowId(string? flowId)
    {
        return flowId is { Length: 32 } &&
            flowId.All(character =>
                character is >= '0' and <= '9' ||
                character is >= 'a' and <= 'f');
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return leftBytes.Length == rightBytes.Length &&
            CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private static async Task<IResult> CheckIfAnyUsersExistAsync(IMediator mediator, CancellationToken cancellationToken)
    {
        return Results.Ok(await mediator.Send(new AnyUsersQuery(), cancellationToken));
    }

    private static async Task<IResult> RegisterAsync(
        RegisterCommand command,
        IValidator<RegisterCommand> validator,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        if (await ValidateRequestAsync(command, validator, cancellationToken) is { } validationResult)
        {
            return validationResult;
        }

        var result = await mediator.Send(command, cancellationToken);
        if (result.Success)
        {
            return Results.Ok(result.Response);
        }

        return result.ErrorCode switch
        {
            AuthErrorCodes.UserAlreadyExists => Conflict(result.Error ?? "User already exists"),
            AuthErrorCodes.RegistrationDisabled => Forbidden(result.Error ?? "Registration is disabled", result.ErrorCode),
            _ => BadRequest(result.Error ?? "Registration failed", result.ErrorCode)
        };
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
        IValidator<CompleteInvitationPasswordRequest> validator,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        if (await ValidateRequestAsync(request, validator, cancellationToken) is { } validationResult)
        {
            return validationResult;
        }

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

    private static async Task<IResult> GetPasswordResetAsync(
        string token,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetPasswordResetQuery(token), cancellationToken);
        return result.Success
            ? Results.Ok(result.PasswordReset)
            : NotFound(result.Error ?? "Password reset link not found", result.ErrorCode);
    }

    private static async Task<IResult> CompletePasswordResetAsync(
        string token,
        ResetPasswordRequest request,
        IValidator<ResetPasswordRequest> validator,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        if (await ValidateRequestAsync(request, validator, cancellationToken) is { } validationResult)
        {
            return validationResult;
        }

        var result = await mediator.Send(
            new CompletePasswordResetCommand(token, request.NewPassword),
            cancellationToken);
        return result.Success
            ? Results.NoContent()
            : NotFound(result.Error ?? "Password reset link not found", result.ErrorCode);
    }

    private static async Task<IResult> GetCurrentAccountAsync(
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetCurrentAccountQuery(), cancellationToken);
        return result.Success
            ? Results.Ok(result.Account)
            : NotFound(result.Error ?? "User not found");
    }

    private static async Task<IResult> ChangePasswordAsync(
        ChangePasswordRequest request,
        IValidator<ChangePasswordRequest> validator,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        if (await ValidateRequestAsync(request, validator, cancellationToken) is { } validationResult)
        {
            return validationResult;
        }

        var result = await mediator.Send(
            new ChangePasswordCommand(request.CurrentPassword, request.NewPassword),
            cancellationToken);
        if (result.Success)
        {
            return Results.NoContent();
        }

        return result.ErrorCode switch
        {
            AuthErrorCodes.PasswordChangeUnavailable =>
                Conflict(result.Error ?? "Password change is unavailable", result.ErrorCode),
            AuthErrorCodes.CurrentPasswordInvalid or AuthErrorCodes.NewPasswordMustDiffer =>
                BadRequest(result.Error ?? "Password could not be changed", result.ErrorCode),
            _ => NotFound(result.Error ?? "User not found")
        };
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
        IValidator<CreateProjectRequest> validator,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        if (await ValidateRequestAsync(request, validator, cancellationToken) is { } validationResult)
        {
            return validationResult;
        }

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

    private static async Task<IResult> RenameProjectAsync(
        string projectId,
        RenameProjectRequest request,
        IValidator<RenameProjectRequest> validator,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        if (await ValidateRequestAsync(request, validator, cancellationToken) is { } validationResult)
        {
            return validationResult;
        }

        var result = await mediator.Send(
            new RenameProjectCommand(projectId, request.Name),
            cancellationToken);
        if (result.Success)
        {
            return Results.Ok(result.Project);
        }

        return result.Error switch
        {
            "Access denied. Only admin users can rename projects." => Forbidden(result.Error),
            "Project not found" => NotFound(result.Error),
            "Project already exists" => Conflict(result.Error),
            _ => BadRequest(result.Error ?? "Project could not be renamed")
        };
    }

    private static async Task<IResult> CreateEnvironmentAsync(
        string projectId,
        CreateEnvironmentRequest request,
        IValidator<CreateEnvironmentRequest> validator,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        if (await ValidateRequestAsync(request, validator, cancellationToken) is { } validationResult)
        {
            return validationResult;
        }

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

    private static async Task<IResult> RenameEnvironmentAsync(
        string projectId,
        string environmentId,
        RenameEnvironmentRequest request,
        IValidator<RenameEnvironmentRequest> validator,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        if (await ValidateRequestAsync(request, validator, cancellationToken) is { } validationResult)
        {
            return validationResult;
        }

        var result = await mediator.Send(
            new RenameEnvironmentCommand(projectId, environmentId, request.Name),
            cancellationToken);
        if (result.Success)
        {
            return Results.Ok(result.Environment);
        }

        return result.Error switch
        {
            "Access denied" => Forbidden(result.Error),
            "Project not found" or "Environment not found" => NotFound(result.Error),
            "Environment already exists" => Conflict(result.Error),
            _ => BadRequest(result.Error ?? "Environment could not be renamed")
        };
    }

    private static async Task<IResult> ListConfigReleasesAsync(
        string projectId,
        string environmentName,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ListConfigReleasesQuery(projectId, environmentName), cancellationToken);
        if (result.Success)
        {
            return Results.Ok(result.Releases);
        }

        return result.Error switch
        {
            "Access denied" => Forbidden(result.Error),
            "Project not found" or "Environment not found" => NotFound(result.Error),
            _ => BadRequest(result.Error ?? "Config releases could not be listed")
        };
    }

    private static async Task<IResult> PublishConfigReleaseAsync(
        string projectId,
        string environmentName,
        PublishConfigReleaseRequest request,
        IValidator<PublishConfigReleaseRequest> validator,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        if (await ValidateRequestAsync(request, validator, cancellationToken) is { } validationResult)
        {
            return validationResult;
        }

        var result = await mediator.Send(
            new PublishConfigReleaseCommand(projectId, environmentName, request.Version, request.MakeActive, request.Entries),
            cancellationToken);

        if (result.Success)
        {
            return Results.Created(
                $"/admin/projects/{projectId}/environments/{environmentName}/releases/{result.Release!.Version}",
                result.Release);
        }

        return result.Error switch
        {
            "Access denied" => Forbidden(result.Error),
            "Project not found" or "Environment not found" => NotFound(result.Error),
            "Release already exists" => Conflict(result.Error),
            _ => BadRequest(result.Error ?? "Config release could not be published")
        };
    }

    private static async Task<IResult> GetConfigReleaseAsync(
        string projectId,
        string environmentName,
        string version,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetConfigReleaseQuery(projectId, environmentName, version), cancellationToken);
        if (result.Success)
        {
            return Results.Ok(result.Release);
        }

        return result.Error switch
        {
            "Access denied" => Forbidden(result.Error),
            "Project not found" or "Environment not found" or "Release not found" => NotFound(result.Error),
            _ => BadRequest(result.Error ?? "Config release not found")
        };
    }

    private static async Task<IResult> DeleteConfigReleaseAsync(
        string projectId,
        string environmentName,
        string version,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new DeleteConfigReleaseCommand(projectId, environmentName, version),
            cancellationToken);

        if (result.Success)
        {
            return Results.NoContent();
        }

        return result.Error switch
        {
            "Access denied" => Forbidden(result.Error),
            "Project not found" or "Environment not found" or "Release not found" => NotFound(result.Error),
            "Active release cannot be deleted" => Conflict(result.Error),
            _ => BadRequest(result.Error ?? "Config release could not be deleted")
        };
    }

    private static async Task<IResult> SetActiveConfigReleaseAsync(
        string projectId,
        string environmentName,
        SetActiveConfigReleaseRequest request,
        IValidator<SetActiveConfigReleaseRequest> validator,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        if (await ValidateRequestAsync(request, validator, cancellationToken) is { } validationResult)
        {
            return validationResult;
        }

        return await SetActiveConfigReleaseAsync(projectId, environmentName, request.Version, mediator, cancellationToken);
    }

    private static async Task<IResult> ClearActiveConfigReleaseAsync(
        string projectId,
        string environmentName,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        return await SetActiveConfigReleaseAsync(projectId, environmentName, null, mediator, cancellationToken);
    }

    private static async Task<IResult> SetActiveConfigReleaseAsync(
        string projectId,
        string environmentName,
        string? version,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new SetActiveConfigReleaseCommand(projectId, environmentName, version), cancellationToken);
        if (result.Success)
        {
            return Results.Ok(result.Environment);
        }

        return result.Error switch
        {
            "Access denied" => Forbidden(result.Error),
            "Project not found" or "Environment not found" or "Release not found" => NotFound(result.Error),
            _ => BadRequest(result.Error ?? "Active config release could not be updated")
        };
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
        IValidator<CreateApiKeyRequest> validator,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        if (await ValidateRequestAsync(request, validator, cancellationToken) is { } validationResult)
        {
            return validationResult;
        }

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
        IValidator<UpsertConfigEntryRequest> validator,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        if (await ValidateRequestAsync(request, validator, cancellationToken) is { } validationResult)
        {
            return validationResult;
        }

        if (!ValidationHelpers.IsValidKey(key))
        {
            return BadRequest(ConfigEntryKey.ValidationError);
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
            return BadRequest(ConfigEntryKey.ValidationError);
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

    private static async Task<IResult> ListParameterShareLinksAsync(
        string projectId,
        string environmentName,
        string key,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new ListParameterShareLinksQuery(projectId, environmentName, key),
            cancellationToken);

        if (result.Success)
        {
            return Results.Ok(result.ShareLinks);
        }

        return result.Error switch
        {
            "Access denied" => Forbidden(result.Error),
            "Project not found" or "Config entry not found" => NotFound(result.Error),
            _ => BadRequest(result.Error ?? "Share links could not be listed")
        };
    }

    private static async Task<IResult> CreateParameterShareLinkAsync(
        string projectId,
        string environmentName,
        string key,
        CreateParameterShareLinkRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new CreateParameterShareLinkCommand(
                projectId,
                environmentName,
                key,
                request.Expiration,
                request.CanEdit),
            cancellationToken);

        if (result.Success)
        {
            return Results.Created(
                $"/admin/projects/{projectId}/environments/{environmentName}/config-entries/{key}/share-links/{result.ShareLink!.Id}",
                result.ShareLink);
        }

        return result.Error switch
        {
            "Access denied" => Forbidden(result.Error),
            "Project not found" or "Environment not found" or "Config entry not found" => NotFound(result.Error),
            _ => BadRequest(result.Error ?? "Share link could not be created")
        };
    }

    private static async Task<IResult> RevokeParameterShareLinkAsync(
        string projectId,
        string environmentName,
        string key,
        long shareLinkId,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new RevokeParameterShareLinkCommand(projectId, environmentName, key, shareLinkId),
            cancellationToken);

        if (result.Success)
        {
            return Results.NoContent();
        }

        return result.Error switch
        {
            "Access denied" => Forbidden(result.Error),
            "Project not found" or "Share link not found" => NotFound(result.Error),
            _ => BadRequest(result.Error ?? "Share link could not be revoked")
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
        IValidator<CreateUserRequest> validator,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        if (await ValidateRequestAsync(request, validator, cancellationToken) is { } validationResult)
        {
            return validationResult;
        }

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
        IValidator<UpdateUserRequest> validator,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        if (await ValidateRequestAsync(request, validator, cancellationToken) is { } validationResult)
        {
            return validationResult;
        }

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

    private static async Task<IResult> GeneratePasswordResetAsync(
        long id,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GeneratePasswordResetCommand(id), cancellationToken);
        if (result.Success)
        {
            return Results.Ok(result.Response);
        }

        return result.Error switch
        {
            "Access denied" => Forbidden(result.Error),
            "User not found" => NotFound(result.Error),
            _ when result.ErrorCode is AuthErrorCodes.PasswordResetUnavailable
                or AuthErrorCodes.PasswordResetSelfNotAllowed =>
                Conflict(result.Error ?? "Password reset is unavailable", result.ErrorCode),
            _ => BadRequest(result.Error ?? "Password reset link could not be generated", result.ErrorCode)
        };
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
        IValidator<ProjectAccessRequest> validator,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        if (await ValidateRequestAsync(request, validator, cancellationToken) is { } validationResult)
        {
            return validationResult;
        }

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

    private static async Task<IResult> ListAuditLogsAsync(
        int? page,
        int? pageSize,
        string? search,
        string? action,
        string? environment,
        DateOnly? dateFrom,
        DateOnly? dateTo,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        return Results.Ok(await mediator.Send(
            new Nona.Application.Admin.AuditLogs.Queries.ListAuditLogsQuery(
                page ?? 1,
                pageSize ?? Nona.Application.Admin.AuditLogs.Queries.ListAuditLogsQueryHandler.DefaultPageSize,
                search,
                action,
                environment,
                dateFrom,
                dateTo),
            cancellationToken));
    }

    private static IResult ExportAuditLogsAsync(
        string? format,
        string? search,
        string? action,
        string? environment,
        DateOnly? dateFrom,
        DateOnly? dateTo,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var normalizedFormat = string.IsNullOrWhiteSpace(format)
            ? "csv"
            : format.Trim().ToLowerInvariant();
        if (normalizedFormat is not ("csv" or "json"))
        {
            return BadRequest("Export format must be csv or json.");
        }

        var logs = mediator.CreateStream(
            new Nona.Application.Admin.AuditLogs.Queries.ExportAuditLogsQuery(
                search,
                action,
                environment,
                dateFrom,
                dateTo),
            cancellationToken);
        var contentType = normalizedFormat == "csv"
            ? "text/csv; charset=utf-8"
            : "application/json; charset=utf-8";
        var fileName = $"audit-logs-{DateTime.UtcNow:yyyy-MM-dd}.{normalizedFormat}";

        return Results.Stream(
            async output =>
            {
                if (normalizedFormat == "csv")
                {
                    await AuditLogExportWriter.WriteCsvAsync(output, logs, cancellationToken);
                }
                else
                {
                    await AuditLogExportWriter.WriteJsonAsync(output, logs, cancellationToken);
                }
            },
            contentType,
            fileName);
    }

    private static async Task<IResult> GetDashboardCountsAsync(IMediator mediator, CancellationToken cancellationToken)
    {
        return Results.Ok(await mediator.Send(new GetDashboardCountsQuery(), cancellationToken));
    }

    public static async Task<IResult> GetSharedParameterAsync(
        string token,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetSharedParameterQuery(token), cancellationToken);
        return result.Success
            ? Results.Ok(result.Parameter)
            : SharedLinkFailure(result.Error, result.ErrorCode);
    }

    public static async Task<IResult> UpdateSharedParameterAsync(
        string token,
        UpdateSharedParameterRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new UpdateSharedParameterCommand(token, request.Value), cancellationToken);
        return result.Success
            ? Results.Ok(result.Parameter)
            : SharedLinkFailure(result.Error, result.ErrorCode);
    }

    public static async Task<IResult> GetConfigValueAsync(
        string environmentId,
        string key,
        string? version,
        HttpContext httpContext,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetConfigEntryValueQuery(environmentId, key, version), cancellationToken);
        if (!result.Success)
        {
            return result.Error switch
            {
                "API key is required" or "Invalid API key" => Unauthorized(result.Error),
                "Version must use major.minor.patch or major.minor.x format." => BadRequest(result.Error),
                _ => NotFound(result.Error ?? "Config value not found")
            };
        }

        httpContext.Response.Headers[NonaResponseHeaders.LogicalContentType] =
            result.LogicalContentType ?? ConfigEntryContentTypes.Text;

        return Results.Content(result.Value!, "application/json");
    }

    public static async Task<IResult> GetAllConfigValuesAsync(
        string environmentId,
        string? version,
        HttpContext httpContext,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new GetAllConfigValuesQuery(
                environmentId,
                version,
                httpContext.Request.Headers.IfNoneMatch.ToString()),
            cancellationToken);
        if (!result.Success)
        {
            return result.Error switch
            {
                "API key is required" or "Invalid API key" => Unauthorized(result.Error),
                "Version must use major.minor.patch or major.minor.x format." => BadRequest(result.Error),
                _ => NotFound(result.Error ?? "Config values not found")
            };
        }

        httpContext.Response.Headers.ETag = result.Etag;
        httpContext.Response.Headers.CacheControl = "private, no-cache";

        if (result.NotModified)
            return Results.StatusCode(StatusCodes.Status304NotModified);

        return Results.Ok(result.Values);
    }

    private static async Task<IResult?> ValidateRequestAsync<TRequest>(
        TRequest request,
        IValidator<TRequest> validator,
        CancellationToken cancellationToken)
    {
        var result = await validator.ValidateAsync(request, cancellationToken);
        if (result.IsValid)
        {
            return null;
        }

        return ApiProblemResults.Validation(result.Errors);
    }

    private static IResult BadRequest(string error, string? errorCode = null)
    {
        return ApiProblemResults.BadRequest(error, errorCode);
    }

    private static IResult Conflict(string error, string? errorCode = null)
    {
        return ApiProblemResults.Conflict(error, errorCode);
    }

    private static IResult NotFound(string error, string? errorCode = null)
    {
        return ApiProblemResults.NotFound(error, errorCode);
    }

    private static IResult Unauthorized(string error, string? errorCode = null)
    {
        return ApiProblemResults.Unauthorized(error, errorCode);
    }

    private static IResult Forbidden(string error, string? errorCode = null)
    {
        return ApiProblemResults.Forbidden(error, errorCode);
    }

    private static IResult Gone(string error, string? errorCode = null)
    {
        return ApiProblemResults.Gone(error, errorCode);
    }

    private static IResult SharedLinkFailure(string? error, string? errorCode)
    {
        var message = error ?? "Share link is not available.";

        return errorCode switch
        {
            ParameterShareLinkErrorCodes.Expired or ParameterShareLinkErrorCodes.Revoked => Gone(message, errorCode),
            ParameterShareLinkErrorCodes.ViewOnly => Forbidden(message, errorCode),
            ParameterShareLinkErrorCodes.Invalid => NotFound(message, errorCode),
            _ => BadRequest(message, errorCode)
        };
    }
}
