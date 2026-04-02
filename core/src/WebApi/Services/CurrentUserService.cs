using Nona.Application.Common.Interfaces;
using Nona.Domain.Entities;
using System.Security.Claims;

namespace Nona.WebApi.Services;

public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    public string? Username => httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Name);

    public bool IsAdmin => httpContextAccessor.HttpContext?.User?.FindFirstValue("isAdmin") == "true";
}
