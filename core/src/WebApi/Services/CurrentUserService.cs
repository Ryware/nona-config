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
            return Enum.TryParse<UserRole>(role, ignoreCase: true, out var parsedRole)
                ? parsedRole
                : null;
        }
    }

    public bool IsAdmin => httpContextAccessor.HttpContext?.User?.FindFirstValue("isAdmin") == "true";
}
