namespace Nona.Application.Admin.Users.DTOs;

public record UserDto(
    long Id,
    string Email,
    string Name,
    string Role,
    string Scope,
    IReadOnlyList<ProjectAccessDto> Projects,
    bool PasswordEnabled,
    DateTime CreatedAt,
    DateTime UpdatedAt);
