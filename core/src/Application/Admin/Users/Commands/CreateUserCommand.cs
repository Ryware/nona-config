using MediatR;
using Nona.Application.Admin.Common;
using Nona.Application.Admin.Users.DTOs;
using Nona.Application.Common;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Entities;
using Nona.Domain.Enums;
using Nona.Domain.Interfaces;

namespace Nona.Application.Admin.Users.Commands;

public record CreateUserRequest(string Name, string Email, string? Role, string? Scope);
public record CreateUserCommand(string Name, string Email, string? Role, string? Scope) : IRequest<CreateUserResult>;
public record CreateUserResult(bool Success, CreateUserResponse? Response, string? Error);

public class CreateUserCommandHandler(
    IUserRepository userRepository,
    IDateTime dateTime,
    IUserAuthorizationService userAuthorizationService,
    IAuditLogService? auditLogService = null) : IRequestHandler<CreateUserCommand, CreateUserResult>
{
    public async Task<CreateUserResult> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        if (!await userAuthorizationService.CanManageUsersAsync(cancellationToken))
            return new CreateUserResult(false, null, "Access denied");

        if (await userRepository.ExistsAsync(request.Email, cancellationToken))
            return new CreateUserResult(false, null, "User already exists");


        var role = ParseRole(request.Role);
        if (role is null && request.Role is not null)
            return new CreateUserResult(false, null, "Invalid role. Must be 'viewer' or 'editor'");


        var scope = ParseScope(request.Scope);
        if (scope is null && request.Scope is not null)
            return new CreateUserResult(false, null, "Invalid scope. Must be 'client', 'server', or 'all'");

        var now = dateTime.NowUtc;

        var invitationToken = TokenHelper.Generate();
        var user = new User
        {
            Email = request.Email,
            InviteTokenHash = TokenHelper.Hash(invitationToken),
            Name = request.Name,
            Role = role ?? UserRole.Viewer,
            Scope = scope ?? KeyScope.All,
            CreatedAt = now,
            UpdatedAt = now
        };

        await userRepository.AddAsync(user, cancellationToken);

        if (auditLogService is not null)
        {
            await auditLogService.WriteAsync(
                "Invited User",
                user.Email,
                cancellationToken: cancellationToken);
        }

        var dto = new UserDto(
            user.Id,
            user.Email,
            user.Name,
            user.Role.ToApiString(),
            user.Scope.ToApiString(),
            user.IsAdmin,
            [],
            user.CreatedAt,
            user.UpdatedAt);

        return new CreateUserResult(true, new CreateUserResponse(dto, invitationToken), null);
    }

    private static UserRole? ParseRole(string? role)
    {
        if (role is null)
            return null;

        return role.ToLowerInvariant() switch
        {
            "viewer" => UserRole.Viewer,
            "editor" => UserRole.Editor,
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
