using MediatR;
using Nona.Application.Auth.DTOs;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Interfaces;
using System.Security.Cryptography;
using System.Text;

namespace Nona.Application.Auth.Commands;

public record LoginCommand(string Username, string Password) : IRequest<LoginResult>;

public record LoginResult(bool Success, LoginResponse? Response, string? Error);

public class LoginCommandHandler(IUserRepository userRepository, IJwtTokenService jwtTokenService) : IRequestHandler<LoginCommand, LoginResult>
{
    public async Task<LoginResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetAsync(request.Username, cancellationToken);

        if (user is null)
            return new LoginResult(false, null, "Invalid username or password");

        if (!VerifyPassword(request.Password, user.PasswordHash, user.PasswordSalt))
            return new LoginResult(false, null, "Invalid username or password");

        var token = jwtTokenService.GenerateToken(user);
        var expiresAt = DateTime.UtcNow.AddHours(24);

        var response = new LoginResponse(
            token,
            user.Username,
            user.Role.ToString().ToLowerInvariant(),
            expiresAt);

        return new LoginResult(true, response, null);
    }

    private static bool VerifyPassword(string password, string? storedHash, string? storedSalt)
    {
        if (string.IsNullOrEmpty(storedHash) || string.IsNullOrEmpty(storedSalt))
            return false;

        var hash = Convert.ToBase64String(
            SHA256.HashData(Encoding.UTF8.GetBytes(password + storedSalt)));

        return hash == storedHash;
    }
}
