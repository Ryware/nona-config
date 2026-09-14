using Nona.Application.Common.Interfaces;

namespace Nona.WebApi.Services;

public class ApiKeyService(IHttpContextAccessor httpContextAccessor) : IApiKeyService
{
    public string? GetCurrentApiKeyHash()
    {
        return httpContextAccessor.HttpContext?.User.FindFirst("ApiKeyHash")?.Value;
    }
}
