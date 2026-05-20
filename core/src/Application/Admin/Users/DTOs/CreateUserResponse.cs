namespace Nona.Application.Admin.Users.DTOs;

public record CreateUserResponse(UserDto User, string InvitationToken);
