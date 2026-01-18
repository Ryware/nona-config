using MediatR;
using Nona.Application.Admin.Users.DTOs;
using Nona.Application.Common;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Entities;
using Nona.Domain.Enums;
using Nona.Domain.Interfaces;

namespace Nona.Application.Admin.Users.Commands;

public record CreateUserRequest(string Username, string Email, string Password, string? Role, string? Scope);
public record CreateUserCommand(string Username, string Email, string Password, string? Role, string? Scope) : IRequest<CreateUserResult>;
public record CreateUserResult(bool Success, UserDto? User, string? Error);

public class CreateUserCommandHandler(IUserRepository userRepository, IDateTime dateTime, IPasswordHasher passwordHasher) : IRequestHandler<CreateUserCommand, CreateUserResult>
{
    public async Task<CreateUserResult> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        if (await userRepository.ExistsAsync(request.Username, cancellationToken))
            return new CreateUserResult(false, null, "User already exists");


        var role = ParseRole(request.Role);
        if (role is null && request.Role is not null)
            return new CreateUserResult(false, null, "Invalid role. Must be 'user' or 'admin'");


        var scope = ParseScope(request.Scope);
        if (scope is null && request.Scope is not null)
            return new CreateUserResult(false, null, "Invalid scope. Must be 'client', 'server', or 'all'");


        var (hash, salt) = passwordHasher.HashPassword(request.Password);
        var now = dateTime.NowUtc;

        var user = new User
        {
            Username = request.Username,
            Email = request.Email,
            PasswordHash = hash,
            PasswordSalt = salt,
            Role = role ?? UserRole.User,
            Scope = scope ?? KeyScope.All,
            CreatedAt = now,
            UpdatedAt = now
        };

        await userRepository.AddAsync(user, cancellationToken);

        var dto = new UserDto(
            user.Username,
            user.Role.ToApiString(),
            user.Scope.ToApiString(),
            [],
            user.CreatedAt,
            user.UpdatedAt);

        return new CreateUserResult(true, dto, null);
    }

    private static UserRole? ParseRole(string? role)
    {
        if (role is null)
            return null;

        return role.ToLowerInvariant() switch
        {
            "user" => UserRole.User,
            "admin" => UserRole.Admin,
            _ => null
        };
    }

    private static KeyScope? ParseScope(string? scope)
    {
        if (scope is null)
            return null;

        return scope.ToLowerInvariant() switch
        {
            "client" => KeyScope.Frontend,
            "server" => KeyScope.Backend,
            "all" => KeyScope.All,
            _ => null
        };
    }
}
