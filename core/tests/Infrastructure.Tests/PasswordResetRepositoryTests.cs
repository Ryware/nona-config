using Nona.Domain.Entities;
using Nona.Domain.Enums;
using Nona.Infrastructure.Repositories.Libsql;
using Nona.Infrastructure.Tests.Common;
using Nona.Libsql;

namespace Nona.Infrastructure.Tests;

[NotInParallel]
public class PasswordResetRepositoryTests
{
    [Test]
    public async Task UserTimestamps_RoundTripAsUtc()
    {
        await using var server = await LocalSqldTestServer.StartAsync();
        using var client = server.CreateClient();
        await new LibsqlMigrationRunner(client, ResolveMigrationsFolder()).RunMigrationsAsync();
        var repository = new LibsqlUserRepository(client);
        var createdAt = new DateTime(2026, 8, 9, 9, 30, 0, DateTimeKind.Utc);
        var updatedAt = createdAt.AddMinutes(15);
        var expiresAt = createdAt.AddHours(24);
        var user = new User
        {
            Email = "utc-reset@example.com",
            Name = "UTC Reset User",
            PasswordHash = "password-hash",
            PasswordSalt = "password-salt",
            PasswordResetTokenHash = "utc-token-hash",
            PasswordResetTokenExpiresAt = expiresAt,
            Role = UserRole.Viewer,
            Scope = KeyScope.All,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
        await repository.AddAsync(user);

        var stored = await repository.GetAsync(user.Email);

        await Assert.That(stored).IsNotNull();
        await Assert.That(stored!.CreatedAt).IsEqualTo(createdAt);
        await Assert.That(stored.CreatedAt.Kind).IsEqualTo(DateTimeKind.Utc);
        await Assert.That(stored.UpdatedAt).IsEqualTo(updatedAt);
        await Assert.That(stored.UpdatedAt.Kind).IsEqualTo(DateTimeKind.Utc);
        await Assert.That(stored.PasswordResetTokenExpiresAt).IsEqualTo(expiresAt);
        await Assert.That(stored.PasswordResetTokenExpiresAt!.Value.Kind).IsEqualTo(DateTimeKind.Utc);
    }

    [Test]
    public async Task ResetToken_IsConsumedExactlyOnce()
    {
        await using var server = await LocalSqldTestServer.StartAsync();
        using var client = server.CreateClient();
        await new LibsqlMigrationRunner(client, ResolveMigrationsFolder()).RunMigrationsAsync();
        var repository = new LibsqlUserRepository(client);
        var now = new DateTime(2026, 8, 9, 12, 0, 0, DateTimeKind.Utc);
        var user = new User
        {
            Email = "reset@example.com",
            Name = "Reset User",
            PasswordHash = "old-hash",
            PasswordSalt = "old-salt",
            PasswordResetTokenHash = "token-hash",
            PasswordResetTokenExpiresAt = now.AddHours(1),
            Role = UserRole.Viewer,
            Scope = KeyScope.All,
            CreatedAt = now,
            UpdatedAt = now
        };
        await repository.AddAsync(user);

        var attempts = await Task.WhenAll(
            repository.TryResetPasswordAsync(
                "token-hash", now, "new-hash-a", "new-salt-a", now, CancellationToken.None),
            repository.TryResetPasswordAsync(
                "token-hash", now, "new-hash-b", "new-salt-b", now, CancellationToken.None));
        var stored = await repository.GetAsync(user.Email);

        await Assert.That(attempts.Count(success => success)).IsEqualTo(1);
        await Assert.That(stored).IsNotNull();
        await Assert.That(stored!.PasswordResetTokenHash).IsNull();
        await Assert.That(stored.PasswordResetTokenExpiresAt).IsNull();
        await Assert.That(stored.PasswordHash is "new-hash-a" or "new-hash-b").IsTrue();
    }

    private static string ResolveMigrationsFolder() => Path.Combine(
        TestPaths.ResolveRepoRoot(),
        "core",
        "src",
        "Infrastructure",
        "Migrations");
}
