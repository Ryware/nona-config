using MediatR;
using Nona.Application.Admin.Users.DTOs;
using Nona.Application.Common;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Entities;
using Nona.Domain.Enums;
using Nona.Domain.Interfaces;

namespace Nona.Application.Admin.Users.Commands;

public record UpdateUserRequest(string? Role, string? Scope);
public record UpdateUserCommand(string Username, string? Role, string? Scope) : IRequest<UpdateUserResult>;
public record UpdateUserResult(bool Success, UserDto? User, string? Error);

public class UpdateUserCommandHandler(IUserRepository userRepository, IProjectMemberRepository projectMemberRepository, IDateTime dateTime) : IRequestHandler<UpdateUserCommand, UpdateUserResult>
{
    public async Task<UpdateUserResult> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetAsync(request.Username, cancellationToken);
        if (user is null)
            return new UpdateUserResult(false, null, "User not found");

        var role = ParseRole(request.Role);
        if (role is null && request.Role is not null)
            return new UpdateUserResult(false, null, "Invalid role. Must be 'user' or 'admin'");

        var scope = ParseScope(request.Scope);
        if (scope is null && request.Scope is not null)
            return new UpdateUserResult(false, null, "Invalid scope. Must be 'client', 'server', or 'all'");


        if (role is not null)
            user.Role = role.Value;

        if (scope is not null)
            user.Scope = scope.Value;

        user.UpdatedAt = dateTime.NowUtc;

        await userRepository.UpdateAsync(user, cancellationToken);

        var members = await projectMemberRepository.ListByUserAsync(user.Email, cancellationToken);
        var projects = members.Select(m => new ProjectAccessDto(m.ProjectName, m.Role.ToApiString())).ToList();

        var dto = new UserDto(
            user.Email,
            user.Role.ToApiString(),
            user.Scope.ToApiString(),
            projects,
            user.CreatedAt,
            user.UpdatedAt);

        return new UpdateUserResult(true, dto, null);
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

    private static (string hash, string salt) HashPassword(string password)
    {
        var salt = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        var hash = Convert.ToBase64String(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(password + salt)));
        return (hash, salt);
    }
}
