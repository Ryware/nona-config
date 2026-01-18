using Nona.Application.Common.Interfaces;

namespace Nona.WebApi.Services;

public class ApiKeyService(IHttpContextAccessor httpContextAccessor) : IApiKeyService
{
    public string? GetCurrentApiKey()
    {
        return httpContextAccessor.HttpContext?.User.FindFirst("ApiKey")?.Value;
    }
}
