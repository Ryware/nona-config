using Nona.Domain.Entities;

namespace Nona.Application.Common.Interfaces;

public interface ICurrentUserService
{
    string? Username { get; }
    UserRole? Role { get; }
    bool IsAdmin { get; }
}
