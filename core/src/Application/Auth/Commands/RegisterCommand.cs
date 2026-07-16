using Mediator;
using Nona.Application.Auth;
using Nona.Application.Auth.DTOs;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Entities;
using Nona.Domain.Enums;
using Nona.Domain.Interfaces;

namespace Nona.Application.Auth.Commands;

public record class RegisterCommand(string Email, string Password) : IRequest<RegisterResult>;

public record RegisterResult(bool Success, LoginResponse? Response, string? Error, string? ErrorCode = null);

public class RegisterCommandHandler(IMediator mediator, IUserRepository userRepository, IDateTime dateTime, IPasswordHasher passwordHasher) : IRequestHandler<RegisterCommand, RegisterResult>
{
    public async ValueTask<RegisterResult> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        if (await userRepository.ExistsAsync(request.Email, cancellationToken))
        {
            return new RegisterResult(false, null, "User already exists", AuthErrorCodes.UserAlreadyExists);
        }

        if (await userRepository.ExistsAnyAsync(cancellationToken))
        {
            return new RegisterResult(false, null, "Registration is disabled", AuthErrorCodes.RegistrationDisabled);
        }

        var (hash, salt) = passwordHasher.HashPassword(request.Password);
        var now = dateTime.NowUtc;

        await userRepository.AddAsync(new User
        {
            Email = request.Email,
            Name = request.Email,
            IsAdmin = true,
            Role = UserRole.Viewer,
            Scope = KeyScope.All,
            PasswordHash = hash,
            PasswordSalt = salt,
            CreatedAt = now,
            UpdatedAt = now
        }, cancellationToken);

        var loginResult = await mediator.Send(new LoginCommand(request.Email, request.Password), cancellationToken);
        if (!loginResult.Success || loginResult.Response is null)
        {
            return new RegisterResult(false, null, loginResult.Error ?? "Registration failed.", loginResult.ErrorCode);
        }

        return new RegisterResult(true, loginResult.Response, null);
    }
}
