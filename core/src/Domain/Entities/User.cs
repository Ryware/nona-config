using Nona.Domain.Enums;

namespace Nona.Domain.Entities;

public class User
{
    public required string Email { get; init; }
    public string? PasswordHash { get; set; }
    public string? PasswordSalt { get; set; }

    public UserRole Role { get; set; } = UserRole.User;
    public KeyScope Scope { get; set; } = KeyScope.All;

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public string? PasswordResetToken { get; set; }

}

public enum UserRole
{
    User,
    Admin
}
