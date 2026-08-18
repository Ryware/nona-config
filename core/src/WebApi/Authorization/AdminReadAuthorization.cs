using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Entities;

namespace Nona.WebApi.Authorization;

public static class AdminReadAuthorizationPolicies
{
    public const string Manage = "ManageAdminReads";
}

public sealed class AdminReadAuthorizationRequirement : IAuthorizationRequirement;

public sealed class AdminReadAuthorizationHandler(IUserAuthorizationService userAuthorizationService)
    : AuthorizationHandler<AdminReadAuthorizationRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AdminReadAuthorizationRequirement requirement)
    {
        var httpContext = context.Resource as HttpContext;
        var currentUser = await userAuthorizationService.GetCurrentUserAsync(
            httpContext?.RequestAborted ?? CancellationToken.None);

        if (currentUser is null)
            return;

        if (currentUser.Role == UserRole.Admin)
            context.Succeed(requirement);
    }
}
