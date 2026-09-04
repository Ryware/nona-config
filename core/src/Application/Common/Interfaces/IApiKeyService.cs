namespace Nona.Application.Common.Interfaces;

public interface IApiKeyService
{
    string? GetCurrentApiKeyHash();
}
