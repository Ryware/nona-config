using Mediator;
using Nona.Application.Admin.Users;
using Nona.Application.Admin.Users.DTOs;
using Nona.Application.Common;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Entities;
using Nona.Domain.Enums;
using Nona.Domain.Interfaces;

namespace Nona.Application.Admin.Users.Commands;

public record UpdateUserRequest(string? Name, string? Role, string? Scope);
public record UpdateUserCommand(long Id, string? Name, string? Role, string? Scope) : IRequest<UpdateUserResult>;
public record UpdateUserResult(bool Success, UserDto? User, string? Error);

public class UpdateUserCommandHandler(
    IUserRepository userRepository,
    IProjectMemberRepository projectMemberRepository,
    IDateTime dateTime,
    IUserAuthorizationService userAuthorizationService,
    IAuditLogService? auditLogService = null) : IRequestHandler<UpdateUserCommand, UpdateUserResult>
{
    public async ValueTask<UpdateUserResult> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        if (!await userAuthorizationService.CanManageUsersAsync(cancellationToken))
            return new UpdateUserResult(false, null, "Access denied");

        var user = await userRepository.GetByIdAsync(request.Id, cancellationToken);
        if (user is null)
            return new UpdateUserResult(false, null, "User not found");

        var currentUser = await userAuthorizationService.GetCurrentUserAsync(cancellationToken);
        var isSelf = string.Equals(user.Email, currentUser?.Email, StringComparison.OrdinalIgnoreCase);

        UserRole? role = null;
        if (request.Role is not null)
        {
            if (!EnumExtensions.TryParseApiRole(request.Role, out var parsedRole))
                return new UpdateUserResult(false, null, "Invalid role. Must be 'admin' or 'member'");

            role = parsedRole;
        }

        if (isSelf && user.Role == UserRole.Admin && role == UserRole.Member)
            return new UpdateUserResult(false, null, "You cannot demote your own admin account");

        if (user.Role == UserRole.Admin && role == UserRole.Member)
        {
            var users = await userRepository.ListAsync(cancellationToken);
            if (users.Count(candidate => candidate.Role == UserRole.Admin) <= 1)
                return new UpdateUserResult(false, null, "At least one admin is required");
        }

        var scope = EnumExtensions.ParseKeyScope(request.Scope);
        if (scope is null && request.Scope is not null)
            return new UpdateUserResult(false, null, "Invalid scope. Must be 'client', 'server', or 'all'");

        var hasChanges = false;

        if (role is not null)
        {
            hasChanges |= user.Role != role.Value;
            user.Role = role.Value;
        }

        if (request.Name is not null && request.Name != user.Name)
        {
            user.Name = request.Name;
            hasChanges = true;
        }

        if (scope is not null)
        {
            hasChanges |= user.Scope != scope.Value;
            user.Scope = scope.Value;
        }

        user.UpdatedAt = dateTime.NowUtc;

        await userRepository.UpdateAsync(user, cancellationToken);

        if (hasChanges && auditLogService is not null)
        {
            await auditLogService.WriteAsync(
                AuditActionKind.Update,
                "Updated User",
                user.Email,
                cancellationToken: cancellationToken);
        }

        var members = await projectMemberRepository.ListByUserAsync(user.Email, cancellationToken);
        var dto = UserDtoMapping.ToDto(user, members);

        return new UpdateUserResult(true, dto, null);
    }
}
