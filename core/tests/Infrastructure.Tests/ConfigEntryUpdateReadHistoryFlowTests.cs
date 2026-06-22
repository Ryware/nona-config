using Nona.Application.Admin.ConfigEntries.Commands;
using Nona.Application.Admin.ConfigEntries.Queries;
using Nona.Application.Api.ConfigEntries.Queries;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Entities;
using Nona.Domain.Enums;
using Nona.Infrastructure.Repositories.InMemory;

namespace Nona.Infrastructure.Tests;

public class ConfigEntryUpdateReadHistoryFlowTests
{
    [Test]
    public async Task AdminUpdate_ThenPublicReadAndHistory_ReturnLatestVersion()
    {
        const string projectName = "test-project";
        const string environmentName = "production";
        const string configKey = "feature.enabled";
        const string apiKeyValue = "api-key-123";

        var projectRepository = new InMemoryProjectRepository();
        var environmentRepository = new InMemoryEnvironmentRepository();
        var configEntryRepository = new InMemoryConfigEntryRepository();
        var apiKeyRepository = new InMemoryApiKeyRepository(projectRepository);
        var accessService = new AllowAllProjectAccessService();
        var currentUser = new MutableCurrentUserService("alice");
        var clock = new MutableDateTime(new DateTime(2026, 6, 21, 10, 0, 0, DateTimeKind.Utc));

        await projectRepository.AddAsync(new Project { Name = projectName });
        await environmentRepository.AddAsync(new ProjectEnvironment { Project = projectName, Name = environmentName });
        await apiKeyRepository.AddAsync(new ApiKey
        {
            Name = "frontend",
            Key = apiKeyValue,
            Project = projectName,
            Scope = KeyScope.Frontend
        });

        var upsertHandler = new UpsertConfigEntryCommandHandler(
            projectRepository,
            environmentRepository,
            configEntryRepository,
            accessService,
            clock,
            currentUserService: currentUser);

        var create = await upsertHandler.Handle(
            new UpsertConfigEntryCommand(projectName, environmentName, configKey, "false", "boolean", "client"),
            CancellationToken.None);

        currentUser.UsernameValue = "bob";
        clock.NowUtcValue = new DateTime(2026, 6, 22, 10, 0, 0, DateTimeKind.Utc);

        var update = await upsertHandler.Handle(
            new UpsertConfigEntryCommand(projectName, environmentName, configKey, "true", "boolean", "client"),
            CancellationToken.None);

        var publicReadHandler = new GetConfigEntryValueQueryHandler(
            apiKeyRepository,
            environmentRepository,
            configEntryRepository,
            new FixedApiKeyService(apiKeyValue));
        var publicRead = await publicReadHandler.Handle(
            new GetConfigEntryValueQuery(environmentName, configKey),
            CancellationToken.None);

        var historyHandler = new ListConfigEntryVersionsQueryHandler(
            projectRepository,
            environmentRepository,
            configEntryRepository,
            accessService);
        var history = await historyHandler.Handle(
            new ListConfigEntryVersionsQuery(projectName, environmentName, configKey),
            CancellationToken.None);

        await Assert.That(create.Success).IsTrue();
        await Assert.That(create.ConfigEntry!.ActiveVersion).IsEqualTo(1);
        await Assert.That(update.Success).IsTrue();
        await Assert.That(update.ConfigEntry!.Value).IsEqualTo("true");
        await Assert.That(update.ConfigEntry.ActiveVersion).IsEqualTo(2);

        await Assert.That(publicRead.Success).IsTrue();
        await Assert.That(publicRead.Value).IsEqualTo("true");
        await Assert.That(publicRead.LogicalContentType).IsEqualTo("boolean");

        await Assert.That(history.Success).IsTrue();
        await Assert.That(history.Versions).Count().IsEqualTo(2);
        await Assert.That(history.Versions![0].Version).IsEqualTo(2);
        await Assert.That(history.Versions[0].Value).IsEqualTo("true");
        await Assert.That(history.Versions[0].Actor).IsEqualTo("bob");
        await Assert.That(history.Versions[1].Version).IsEqualTo(1);
        await Assert.That(history.Versions[1].Value).IsEqualTo("false");
        await Assert.That(history.Versions[1].Actor).IsEqualTo("alice");
    }

    private sealed class AllowAllProjectAccessService : IProjectAccessService
    {
        public Task<bool> HasViewAccessAsync(string projectName, CancellationToken ct = default)
            => Task.FromResult(true);

        public Task<bool> HasEditAccessAsync(string projectName, CancellationToken ct = default)
            => Task.FromResult(true);
    }

    private sealed class FixedApiKeyService(string apiKey) : IApiKeyService
    {
        public string? GetCurrentApiKey() => apiKey;
    }

    private sealed class MutableCurrentUserService(string username) : ICurrentUserService
    {
        public string? UsernameValue { get; set; } = username;
        public string? Username => UsernameValue;
        public UserRole? Role => UserRole.Viewer;
        public bool IsAdmin => true;
    }

    private sealed class MutableDateTime(DateTime nowUtc) : IDateTime
    {
        public DateTime NowUtcValue { get; set; } = nowUtc;
        public DateTime NowUtc => NowUtcValue;
    }
}
