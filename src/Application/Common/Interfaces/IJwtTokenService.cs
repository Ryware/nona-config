using Nona.Domain.Entities;

namespace Nona.Application.Common.Interfaces;

public interface IJwtTokenService
{
    string GenerateToken(User user);
}
