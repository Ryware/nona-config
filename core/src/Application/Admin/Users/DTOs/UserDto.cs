namespace Nona.Application.Admin.Users.DTOs;

public record UserDto(string Email, string Role, string Scope, IReadOnlyList<ProjectAccessDto> Projects, DateTime CreatedAt, DateTime UpdatedAt, string? ResetPasswordToken = null);
