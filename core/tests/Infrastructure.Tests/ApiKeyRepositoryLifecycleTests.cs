using Nona.Application.Common;
using Nona.Domain.Entities;
using Nona.Domain.Enums;
using Nona.Infrastructure.Repositories.InMemory;

namespace Nona.Infrastructure.Tests;

public class ApiKeyRepositoryLifecycleTests
{
    [Test]
    public async Task ReplacementAndDeletion_KeepNewCredentialValid()
    {
        const string oldSecret = "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";
        const string newSecret = "FEDCBA9876543210FEDCBA9876543210FEDCBA9876543210FEDCBA9876543210";
        var projectRepository = new InMemoryProjectRepository();
        var apiKeyRepository = new InMemoryApiKeyRepository(projectRepository);
        var createdAt = new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        await projectRepository.AddAsync(new Project { Name = "alpha" });
        var oldApiKey = new ApiKey
        {
            Name = "Deployment",
            KeyHash = ApiKeySecret.Hash(oldSecret),
            Fingerprint = ApiKeySecret.Fingerprint(oldSecret),
            Project = "alpha",
            Environment = "production",
            Scope = KeyScope.Backend,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };
        var replacementApiKey = new ApiKey
        {
            Name = "Deployment replacement",
            KeyHash = ApiKeySecret.Hash(newSecret),
            Fingerprint = ApiKeySecret.Fingerprint(newSecret),
            Project = "alpha",
            Environment = "production",
            Scope = KeyScope.Backend,
            CreatedAt = createdAt.AddDays(1),
            UpdatedAt = createdAt.AddDays(1)
        };
        await apiKeyRepository.AddAsync(oldApiKey);
        await apiKeyRepository.AddAsync(replacementApiKey);

        await Assert.That(await apiKeyRepository.GetByKeyHashAsync(ApiKeySecret.Hash(oldSecret))).IsNotNull();
        await Assert.That(await apiKeyRepository.GetByKeyHashAsync(ApiKeySecret.Hash(newSecret))).IsNotNull();

        await apiKeyRepository.DeleteAsync(oldApiKey.Id);

        await Assert.That(await apiKeyRepository.GetByKeyHashAsync(ApiKeySecret.Hash(oldSecret))).IsNull();
        await Assert.That(await apiKeyRepository.GetByKeyHashAsync(ApiKeySecret.Hash(newSecret))).IsNotNull();
        await Assert.That(await apiKeyRepository.GetByIdAsync(oldApiKey.Id)).IsNull();
        await Assert.That(await apiKeyRepository.GetByIdAsync(replacementApiKey.Id)).IsNotNull();
    }
}
