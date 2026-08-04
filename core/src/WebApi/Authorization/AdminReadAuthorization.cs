using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Entities;

namespace Nona.WebApi.Authorization;

public static class AdminReadAuthorizationPolicies
{
    public const string Manage = "ManageAdminReads";
    public const string SelfOrManageUser = "SelfOrManageUser";
}

public sealed class AdminReadAuthorizationRequirement(bool allowSelf) : IAuthorizationRequirement
{
    public bool AllowSelf { get; } = allowSelf;
}

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

        if (currentUser.Role is UserRole.Admin or UserRole.Editor)
        {
            context.Succeed(requirement);
            return;
        }

        if (!requirement.AllowSelf || httpContext is null)
            return;

        var routeId = httpContext.Request.RouteValues["id"]?.ToString();
        if (long.TryParse(routeId, out var targetUserId) && currentUser.Id == targetUserId)
            context.Succeed(requirement);
    }
}
