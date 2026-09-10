using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Nona.Application.Common.Interfaces;
using Nona.WebApi.Authentication;
using Nona.WebApi.Authorization;
using Nona.WebApi.Endpoints;
using Nona.WebApi.Services;
using System.Text;
using System.Security.Claims;
using Nona.Domain.Interfaces;
using Nona.Infrastructure.Services;

namespace Nona.WebApi;

public static class ConfigureServices
{
    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddHttpClient();

        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IApiKeyService, ApiKeyService>();
        services.AddScoped<IAuthorizationHandler, AdminReadAuthorizationHandler>();
        services.AddProblemDetails();
        services.AddExceptionHandler<ApiExceptionHandler>();

        var authentication = services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        });

        authentication.AddJwtBearer(options =>
        {
            var jwtKey = configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key is not configured");
            var jwtIssuer = configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("JWT Issuer is not configured");
            var jwtAudience = configuration["Jwt:Audience"] ?? throw new InvalidOperationException("JWT Audience is not configured");

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtIssuer,
                ValidAudience = jwtAudience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
            };

            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    var email = context.Principal?.FindFirstValue(ClaimTypes.Name);
                    var stamp = context.Principal?.FindFirstValue(JwtTokenService.CredentialStampClaim);
                    var repository = context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
                    var user = string.IsNullOrEmpty(email) ? null
                        : await repository.GetAsync(email, context.HttpContext.RequestAborted);
                    if (user is null || string.IsNullOrEmpty(stamp)
                        || !string.Equals(stamp, JwtTokenService.GetCredentialStamp(user, jwtKey), StringComparison.Ordinal))
                    {
                        context.Fail("The session is no longer valid. Sign in again.");
                    }
                },
                OnChallenge = async context =>
                {
                    context.HandleResponse();
                    await ApiProblemResults
                        .Unauthorized("Authentication is required.")
                        .ExecuteAsync(context.HttpContext);
                },
                OnForbidden = context => ApiProblemResults
                    .Forbidden("You do not have permission to access this resource.")
                    .ExecuteAsync(context.HttpContext)
            };
        });

        authentication.AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
            ApiKeyAuthenticationHandler.SchemeName, null);

        services.AddAuthorizationBuilder()
            .AddPolicy(ApiKeyAuthenticationHandler.SchemeName, policy => policy
                .AddAuthenticationSchemes(ApiKeyAuthenticationHandler.SchemeName)
                .RequireAuthenticatedUser())
            .AddPolicy(AdminReadAuthorizationPolicies.Manage, policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new AdminReadAuthorizationRequirement()));

        return services;
    }
}
