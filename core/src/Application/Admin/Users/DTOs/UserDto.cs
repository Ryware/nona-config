namespace Nona.Application.Admin.Users.DTOs;

public record UserDto(long Id, string Email, string Name, string Role, string Scope, bool IsAdmin, IReadOnlyList<ProjectAccessDto> Projects, DateTime CreatedAt, DateTime UpdatedAt, string? ResetPasswordToken = null);
