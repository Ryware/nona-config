namespace Nona.Application.Admin.Users.DTOs;

public record GeneratePasswordResetResponse(string PasswordResetToken, DateTime ExpiresAt);
