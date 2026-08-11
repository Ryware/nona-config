namespace Nona.Application.Auth.DTOs;

public record PasswordResetDetailsResponse(string Email, string Name, DateTime ExpiresAt);
