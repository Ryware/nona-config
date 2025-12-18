using Nona.Domain.Entities;
using Nona.Domain.Interfaces;
using System.Security.Cryptography;
using System.Text;

namespace Nona.Infrastructure.Seeding;

public class DataSeeder(IUserRepository userRepository)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        await SeedDefaultAdminUserAsync(ct);
    }

    private async Task SeedDefaultAdminUserAsync(CancellationToken ct)
    {
        const string defaultUsername = "admin";
        const string defaultPassword = "admin";

        if (await userRepository.ExistsAsync(defaultUsername, ct))
            return;

        var (hash, salt) = HashPassword(defaultPassword);
        var now = DateTime.UtcNow;

        var adminUser = new User
        {
            Username = defaultUsername,
            PasswordHash = hash,
            PasswordSalt = salt,
            Role = UserRole.Admin,
            Scope = Nona.Domain.Enums.KeyScope.All,
            CreatedAt = now,
            UpdatedAt = now
        };

        await userRepository.AddAsync(adminUser, ct);
    }

    private static (string hash, string salt) HashPassword(string password)
    {
        var salt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var hash = Convert.ToBase64String(
            SHA256.HashData(Encoding.UTF8.GetBytes(password + salt)));
        return (hash, salt);
    }
}
