using Nona.Domain.Entities;
using Nona.Domain.Enums;
using Nona.Infrastructure.Repositories.InMemory;

namespace Nona.Infrastructure.Tests;

public class InMemoryConfigEntryRepositoryTests
{
    [Test]
    public async Task AddVersionAsync_RejectsInvalidKey()
    {
        var repository = new InMemoryConfigEntryRepository();
        Exception? exception = null;
        try
        {
            await repository.AddVersionAsync(new ConfigEntry
            {
                Project = "test-project",
                Environment = "production",
                Key = "feature/value",
                Value = "true",
                ContentType = "boolean",
                Scope = KeyScope.All
            }, "alice");
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        await Assert.That(exception).IsTypeOf<ArgumentException>();
        await Assert.That(await repository.CountAsync()).IsEqualTo(0);
    }

    [Test]
    public async Task AddVersionAsync_AppendsHistoryAndRollbackProducesNewActiveVersion()
    {
        var repository = new InMemoryConfigEntryRepository();
        var project = "test-project";
        var environment = "production";
        var key = "feature.enabled";
        var firstAt = new DateTime(2026, 6, 20, 10, 0, 0, DateTimeKind.Utc);
        var secondAt = new DateTime(2026, 6, 21, 10, 0, 0, DateTimeKind.Utc);
        var rollbackAt = new DateTime(2026, 6, 22, 10, 0, 0, DateTimeKind.Utc);

        await repository.AddVersionAsync(new ConfigEntry
        {
            Project = project,
            Environment = environment,
            Key = key,
            Value = "false",
            ContentType = "boolean",
            Scope = KeyScope.Frontend,
            CreatedAt = firstAt,
            UpdatedAt = firstAt
        }, "alice");

        var current = await repository.AddVersionAsync(new ConfigEntry
        {
            Project = project,
            Environment = environment,
            Key = key,
            Value = "true",
            ContentType = "boolean",
            Scope = KeyScope.Backend,
            CreatedAt = firstAt,
            UpdatedAt = secondAt
        }, "bob");

        await Assert.That(current!.ActiveVersion).IsEqualTo(2);
        await Assert.That(current.Value).IsEqualTo("true");

        var targetVersion = await repository.GetVersionAsync(project, environment, key, 1);
        var rollback = await repository.AddVersionAsync(new ConfigEntry
        {
            Project = project,
            Environment = environment,
            Key = key,
            Value = targetVersion!.Value,
            ContentType = targetVersion.ContentType,
            Scope = targetVersion.Scope,
            CreatedAt = firstAt,
            UpdatedAt = rollbackAt
        }, "carol");

        await Assert.That(rollback!.ActiveVersion).IsEqualTo(3);
        await Assert.That(rollback.Value).IsEqualTo("false");

        var versions = await repository.ListVersionsAsync(project, environment, key);
        await Assert.That(versions).Count().IsEqualTo(3);
        await Assert.That(versions[0].Version).IsEqualTo(3);
        await Assert.That(versions[1].Version).IsEqualTo(2);
        await Assert.That(versions[2].Version).IsEqualTo(1);
        await Assert.That(versions[0].Value).IsEqualTo("false");
        await Assert.That(versions[0].Actor).IsEqualTo("carol");
        await Assert.That(versions[1].Value).IsEqualTo("true");
        await Assert.That(versions[1].Actor).IsEqualTo("bob");
        await Assert.That(versions[2].Scope).IsEqualTo(KeyScope.Frontend);
        await Assert.That(versions[2].Actor).IsEqualTo("alice");
    }
}
