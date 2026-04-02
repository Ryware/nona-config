using Nona.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nona.Domain.Entities;

public class User
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    public required string Email { get; init; }
    public required string Name { get; set; }
    public string? PasswordHash { get; set; }
    public string? PasswordSalt { get; set; }

    public bool IsAdmin { get; set; }

    public UserRole Role { get; set; } = UserRole.Viewer;
    public KeyScope Scope { get; set; } = KeyScope.All;

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public string? PasswordResetToken { get; set; }

}

public enum UserRole
{
    Viewer,
    Editor
}
