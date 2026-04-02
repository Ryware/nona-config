using MediatR;
using Nona.Application.Auth.DTOs;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Entities;
using Nona.Domain.Enums;
using Nona.Domain.Interfaces;

namespace Nona.Application.Auth.Commands;

public record class RegisterCommand(string Email, string Password) : IRequest<RegisterResult>;

public record RegisterResult(bool Success, LoginResponse? Response, string? Error);

internal class RegisterCommandHandler(IMediator mediator, IUserRepository userRepository, IDateTime dateTime, IPasswordHasher passwordHasher) : IRequestHandler<RegisterCommand, RegisterResult>
{
    public async Task<RegisterResult> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {

        var exists = await userRepository.ExistsAnyAsync(cancellationToken);

        if (exists)
        {
            return new RegisterResult(false, null, "User already exists.");
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
        return new RegisterResult(true, loginResult.Response, null);
    }
}
