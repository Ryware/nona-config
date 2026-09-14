using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nona.Domain.Entities;
using Nona.Domain.Interfaces;
using Nona.Infrastructure.Configuration;
using Nona.Infrastructure.Services;
using Nona.Libsql;

namespace Nona.Infrastructure.Tests;

public class ProjectCredentialDeletionTests
{
    [Test]
    [Arguments("InMemory")]
    [Arguments("Sqlite")]
    public async Task RecreatedProjectCannotReuseCredentials(string storage)
    {
        var path = Path.Combine(Path.GetTempPath(), $"nona-credentials-{Guid.NewGuid():N}.db");
        try
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:Type"] = storage,
                ["Storage:Sqlite:DataSource"] = path
            }).Build();
            using var services = new ServiceCollection().AddStorageProvider(configuration).BuildServiceProvider();
            if (storage == "Sqlite")
                await new LibsqlDatabaseInitializer(services.GetRequiredService<ILibsqlDatabaseClient>()).StartAsync(default);
            var users = services.GetRequiredService<IUserRepository>();
            var user = new User
            {
                Email = "admin@example.com",
                Name = "Admin",
                PasswordHash = "old-hash",
                CreatedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Unspecified),
                PasswordResetTokenHash = "reset",
                PasswordResetTokenExpiresAt = DateTime.UtcNow.AddHours(1)
            };
            await users.AddAsync(user);
            var stamp = JwtTokenService.GetCredentialStamp(user, "test-signing-key");
            var stored = (await users.GetAsync(user.Email))!;
            await Assert.That(JwtTokenService.GetCredentialStamp(stored, "test-signing-key")).IsEqualTo(stamp);
            stored.Name = "Renamed";
            await users.UpdateAsync(stored);
            await Assert.That(JwtTokenService.GetCredentialStamp((await users.GetAsync(user.Email))!, "test-signing-key")).IsEqualTo(stamp);
            await Assert.That(await users.TryResetPasswordAsync("reset", DateTime.UtcNow, "new-hash", "", DateTime.UtcNow)).IsTrue();
            await Assert.That(JwtTokenService.GetCredentialStamp((await users.GetAsync(user.Email))!, "test-signing-key")).IsNotEqualTo(stamp);
            var projects = services.GetRequiredService<IProjectRepository>();
            var keys = services.GetRequiredService<IApiKeyRepository>();
            var links = services.GetRequiredService<IParameterShareLinkRepository>();
            await projects.AddAsync(new Project { Name = "Alpha", UrlSlug = "alpha" });
            await projects.AddAsync(new Project { Name = "Other", UrlSlug = "other" });
            foreach (var project in new[] { "Alpha", "Other" })
            {
                await keys.AddAsync(new ApiKey { Name = project, Project = project, KeyHash = project, Fingerprint = project });
                await links.AddAsync(new ParameterShareLink
                {
                    Project = project,
                    Environment = "production",
                    Key = "flag",
                    Token = project,
                    TokenHash = project,
                    CreatedBy = "admin@example.com",
                    CanEdit = true,
                    ExpiresAt = DateTime.UtcNow.AddDays(1)
                });
            }
            await Assert.That(await keys.GetByKeyHashAsync("Alpha")).IsNotNull();
            await Assert.That(await links.GetByTokenHashAsync("Alpha")).IsNotNull();
            await projects.DeleteAsync("ALPHA");
            await projects.AddAsync(new Project { Name = "alpha", UrlSlug = "alpha" });
            await Assert.That(await keys.GetByKeyHashAsync("Alpha")).IsNull();
            await Assert.That(await links.GetByTokenHashAsync("Alpha")).IsNull();
            await Assert.That(await keys.GetByKeyHashAsync("Other")).IsNotNull();
            await Assert.That(await links.GetByTokenHashAsync("Other")).IsNotNull();
            await keys.AddAsync(new ApiKey { Name = "fresh", Project = "alpha", KeyHash = "fresh", Fingerprint = "fresh" });
            await links.AddAsync(new ParameterShareLink
            {
                Project = "alpha",
                Environment = "production",
                Key = "flag",
                Token = "fresh",
                TokenHash = "fresh",
                CreatedBy = "admin@example.com",
                CanEdit = true,
                ExpiresAt = DateTime.UtcNow.AddDays(1)
            });
            await Assert.That(await keys.GetByKeyHashAsync("fresh")).IsNotNull();
            await Assert.That(await links.GetByTokenHashAsync("fresh")).IsNotNull();
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }
}
