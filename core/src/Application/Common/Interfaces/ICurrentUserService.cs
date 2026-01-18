namespace Nona.Application.Common.Interfaces;

public interface ICurrentUserService
{
    string? Username { get; }
    bool IsAdmin { get; }
}
