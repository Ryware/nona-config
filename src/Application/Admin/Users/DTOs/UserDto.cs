namespace Nona.Application.Admin.Users.DTOs;

public record UserDto(string Username, string Role, string Scope, IReadOnlyList<ProjectAccessDto> Projects, DateTime CreatedAt, DateTime UpdatedAt);
