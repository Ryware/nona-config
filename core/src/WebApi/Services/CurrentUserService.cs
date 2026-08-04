using Nona.Application.Common.Interfaces;
using Nona.Domain.Entities;
using System.Security.Claims;

namespace Nona.WebApi.Services;

public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    public string? Username => httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Name);

    public UserRole? Role
    {
        get
        {
            var role = httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Role);
            if (Enum.TryParse<UserRole>(role, ignoreCase: true, out var parsedRole))
            {
                // Tokens issued before the explicit Admin role used Viewer + isAdmin=true.
                // Keep those short-lived sessions working while making Role authoritative.
                if (parsedRole == UserRole.Viewer
                    && httpContextAccessor.HttpContext?.User?.FindFirstValue("isAdmin") == "true")
                {
                    return UserRole.Admin;
                }

                return parsedRole;
            }

            return null;
        }
    }
}
