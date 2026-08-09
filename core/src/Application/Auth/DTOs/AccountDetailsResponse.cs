namespace Nona.Application.Auth.DTOs;

public record AccountDetailsResponse(
    string Email,
    string Name,
    string Role,
    bool PasswordEnabled);
