using MediatR;
using Nona.Application.Auth.DTOs;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Interfaces;

namespace Nona.Application.Auth.Commands;

public record LoginCommand(string Email, string Password) : IRequest<LoginResult>;

public record LoginResult(bool Success, LoginResponse? Response, string? Error);

public class LoginCommandHandler(IUserRepository userRepository, IJwtTokenService jwtTokenService, IPasswordHasher passwordHasher) : IRequestHandler<LoginCommand, LoginResult>
{
    public async Task<LoginResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetAsync(request.Email, cancellationToken);

        if (user is null)
            return new LoginResult(false, null, "Invalid username or password");

        if (!passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
            return new LoginResult(false, null, "Invalid username or password");

        var token = jwtTokenService.GenerateToken(user);
        var expiresAt = DateTime.UtcNow.AddHours(24);

        var response = new LoginResponse(
            token,
            user.Email,
            user.Role.ToString().ToLowerInvariant(),
            expiresAt);

        return new LoginResult(true, response, null);
    }
}
