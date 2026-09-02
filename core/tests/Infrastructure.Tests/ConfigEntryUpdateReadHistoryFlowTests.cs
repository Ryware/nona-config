using Nona.Application.Admin.ConfigReleases.Commands;
using Nona.Application.Admin.ConfigEntries.Commands;
using Nona.Application.Admin.ConfigEntries.Queries;
using Nona.Application.Api.ConfigEntries.Queries;
using Nona.Application.Common.Interfaces;
using Nona.Application.Shared.ParameterShareLinks.Commands;
using Nona.Application.Shared.ParameterShareLinks.Queries;
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
        const string configKey = "Features:Checkout";
        const string apiKeyValue = "api-key-123";

        var projectRepository = new InMemoryProjectRepository();
        var environmentRepository = new InMemoryEnvironmentRepository();
        var configEntryRepository = new InMemoryConfigEntryRepository();
        var configReleaseRepository = new InMemoryConfigReleaseRepository();
        var apiKeyRepository = new InMemoryApiKeyRepository(projectRepository);
        var accessService = new AllowAllProjectAccessService();
        var currentUser = new MutableCurrentUserService("alice");
        var clock = new MutableDateTime(new DateTime(2026, 6, 21, 10, 0, 0, DateTimeKind.Utc));

        await projectRepository.AddAsync(new Project { Name = projectName });
        await environmentRepository.AddAsync(new ProjectEnvironment { Project = projectName, Name = environmentName });
        await apiKeyRepository.AddAsync(new ApiKey
        {
            Name = "frontend",
            KeyHash = apiKeyValue,
            Fingerprint = "key-123",
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

        var publishHandler = new PublishConfigReleaseCommandHandler(
            projectRepository,
            environmentRepository,
            configEntryRepository,
            configReleaseRepository,
            accessService,
            clock,
            currentUser);
        var publish = await publishHandler.Handle(
            new PublishConfigReleaseCommand(projectName, environmentName, "1.0.0", MakeActive: true),
            CancellationToken.None);

        var publicReadHandler = new GetConfigEntryValueQueryHandler(
            apiKeyRepository,
            environmentRepository,
            configEntryRepository,
            configReleaseRepository,
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
        await Assert.That(publish.Success).IsTrue();

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

    [Test]
    public async Task PublishedReleases_RemainImmutableAndResolveExactLineAndActiveVersions()
    {
        const string projectName = "test-project";
        const string environmentName = "production";
        const string configKey = "Features:Checkout";
        const string apiKeyValue = "api-key-123";

        var projectRepository = new InMemoryProjectRepository();
        var environmentRepository = new InMemoryEnvironmentRepository();
        var configEntryRepository = new InMemoryConfigEntryRepository();
        var configReleaseRepository = new InMemoryConfigReleaseRepository();
        var apiKeyRepository = new InMemoryApiKeyRepository(projectRepository);
        var accessService = new AllowAllProjectAccessService();
        var currentUser = new MutableCurrentUserService("alice");
        var clock = new MutableDateTime(new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc));

        await projectRepository.AddAsync(new Project { Name = projectName });
        await environmentRepository.AddAsync(new ProjectEnvironment { Project = projectName, Name = environmentName });
        await apiKeyRepository.AddAsync(new ApiKey
        {
            Name = "frontend",
            KeyHash = apiKeyValue,
            Fingerprint = "key-123",
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
        var publishHandler = new PublishConfigReleaseCommandHandler(
            projectRepository,
            environmentRepository,
            configEntryRepository,
            configReleaseRepository,
            accessService,
            clock,
            currentUser);
        var publicReadHandler = new GetConfigEntryValueQueryHandler(
            apiKeyRepository,
            environmentRepository,
            configEntryRepository,
            configReleaseRepository,
            new FixedApiKeyService(apiKeyValue));

        await upsertHandler.Handle(
            new UpsertConfigEntryCommand(projectName, environmentName, configKey, "false", "boolean", "client"),
            CancellationToken.None);
        await publishHandler.Handle(
            new PublishConfigReleaseCommand(projectName, environmentName, "1.0.0", MakeActive: true),
            CancellationToken.None);

        clock.NowUtcValue = new DateTime(2026, 7, 1, 11, 0, 0, DateTimeKind.Utc);
        await upsertHandler.Handle(
            new UpsertConfigEntryCommand(projectName, environmentName, configKey, "true", "boolean", "client"),
            CancellationToken.None);
        await publishHandler.Handle(
            new PublishConfigReleaseCommand(projectName, environmentName, "1.0.1", MakeActive: true),
            CancellationToken.None);

        var exactOld = await publicReadHandler.Handle(
            new GetConfigEntryValueQuery(environmentName, configKey, "1.0.0"),
            CancellationToken.None);
        var lineLatest = await publicReadHandler.Handle(
            new GetConfigEntryValueQuery(environmentName, configKey, "1.0.x"),
            CancellationToken.None);
        var active = await publicReadHandler.Handle(
            new GetConfigEntryValueQuery(environmentName, configKey),
            CancellationToken.None);

        clock.NowUtcValue = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);
        await upsertHandler.Handle(
            new UpsertConfigEntryCommand(projectName, environmentName, configKey, "working", "text", "client"),
            CancellationToken.None);
        var clearActiveHandler = new SetActiveConfigReleaseCommandHandler(
            projectRepository,
            environmentRepository,
            configReleaseRepository,
            accessService,
            clock);
        var clearActive = await clearActiveHandler.Handle(
            new SetActiveConfigReleaseCommand(projectName, environmentName, null),
            CancellationToken.None);
        var workingFallback = await publicReadHandler.Handle(
            new GetConfigEntryValueQuery(environmentName, configKey),
            CancellationToken.None);

        await Assert.That(exactOld.Success).IsTrue();
        await Assert.That(exactOld.Value).IsEqualTo("false");
        await Assert.That(lineLatest.Success).IsTrue();
        await Assert.That(lineLatest.Value).IsEqualTo("true");
        await Assert.That(active.Success).IsTrue();
        await Assert.That(active.Value).IsEqualTo("true");
        await Assert.That(clearActive.Success).IsTrue();
        await Assert.That(workingFallback.Success).IsTrue();
        await Assert.That(workingFallback.Value).IsEqualTo("working");
    }

    [Test]
    public async Task SharedUpdate_ChangesDefaultOnlyWhileActiveReleaseRemainsInUse()
    {
        const string projectName = "test-project";
        const string environmentName = "production";
        const string configKey = "Features:Checkout";
        const string apiKeyValue = "api-key-123";
        const string token = "AbCdEf1234567890";

        var projectRepository = new InMemoryProjectRepository();
        var environmentRepository = new InMemoryEnvironmentRepository();
        var configEntryRepository = new InMemoryConfigEntryRepository();
        var configReleaseRepository = new InMemoryConfigReleaseRepository();
        var shareLinkRepository = new InMemoryParameterShareLinkRepository();
        var apiKeyRepository = new InMemoryApiKeyRepository(projectRepository);
        var accessService = new AllowAllProjectAccessService();
        var currentUser = new MutableCurrentUserService("alice");
        var clock = new MutableDateTime(new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc));

        await projectRepository.AddAsync(new Project { Name = projectName });
        await environmentRepository.AddAsync(new ProjectEnvironment { Project = projectName, Name = environmentName });
        await apiKeyRepository.AddAsync(new ApiKey
        {
            Name = "frontend",
            KeyHash = apiKeyValue,
            Fingerprint = "key-123",
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
        var publishHandler = new PublishConfigReleaseCommandHandler(
            projectRepository,
            environmentRepository,
            configEntryRepository,
            configReleaseRepository,
            accessService,
            clock,
            currentUser);

        await upsertHandler.Handle(
            new UpsertConfigEntryCommand(projectName, environmentName, configKey, "release-value", "text", "client"),
            CancellationToken.None);
        await publishHandler.Handle(
            new PublishConfigReleaseCommand(projectName, environmentName, "1.11.1", MakeActive: true),
            CancellationToken.None);

        clock.NowUtcValue = clock.NowUtcValue.AddHours(1);
        await upsertHandler.Handle(
            new UpsertConfigEntryCommand(projectName, environmentName, configKey, "default-value", "text", "client"),
            CancellationToken.None);
        await shareLinkRepository.AddAsync(new ParameterShareLink
        {
            TokenHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(token))),
            Token = token,
            Project = projectName,
            Environment = environmentName,
            Key = configKey,
            CanEdit = true,
            CreatedBy = "alice",
            CreatedAt = clock.NowUtc,
            ExpiresAt = clock.NowUtc.AddHours(1)
        });

        var sharedGetHandler = new GetSharedParameterQueryHandler(
            shareLinkRepository,
            configEntryRepository,
            clock);
        var sharedUpdateHandler = new UpdateSharedParameterCommandHandler(
            shareLinkRepository,
            configEntryRepository,
            clock);
        var publicReadHandler = new GetConfigEntryValueQueryHandler(
            apiKeyRepository,
            environmentRepository,
            configEntryRepository,
            configReleaseRepository,
            new FixedApiKeyService(apiKeyValue));

        var sharedBeforeUpdate = await sharedGetHandler.Handle(
            new GetSharedParameterQuery(token),
            CancellationToken.None);
        var sharedUpdate = await sharedUpdateHandler.Handle(
            new UpdateSharedParameterCommand(token, "shared-default-value"),
            CancellationToken.None);
        var publicRead = await publicReadHandler.Handle(
            new GetConfigEntryValueQuery(environmentName, configKey),
            CancellationToken.None);
        var savedDefault = await configEntryRepository.GetAsync(projectName, environmentName, configKey);
        var activeRelease = await configReleaseRepository.GetAsync(projectName, environmentName, "1.11.1");
        var releases = await configReleaseRepository.ListAsync(projectName, environmentName);
        var environment = await environmentRepository.GetAsync(projectName, environmentName);

        await Assert.That(sharedBeforeUpdate.Success).IsTrue();
        await Assert.That(sharedBeforeUpdate.Parameter!.Value).IsEqualTo("default-value");
        await Assert.That(sharedUpdate.Success).IsTrue();
        await Assert.That(savedDefault!.Value).IsEqualTo("shared-default-value");
        await Assert.That(savedDefault.ActiveVersion).IsEqualTo(3);
        await Assert.That(publicRead.Success).IsTrue();
        await Assert.That(publicRead.Value).IsEqualTo("release-value");
        await Assert.That(activeRelease!.Entries.Single().Value).IsEqualTo("release-value");
        await Assert.That(releases).Count().IsEqualTo(1);
        await Assert.That(releases.Single().Version).IsEqualTo("1.11.1");
        await Assert.That(environment!.ActiveReleaseVersion).IsEqualTo("1.11.1");
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
        public string? GetCurrentApiKeyHash() => apiKey;
    }

    private sealed class MutableCurrentUserService(string username) : ICurrentUserService
    {
        public string? UsernameValue { get; set; } = username;
        public string? Username => UsernameValue;
    }

    private sealed class MutableDateTime(DateTime nowUtc) : IDateTime
    {
        public DateTime NowUtcValue { get; set; } = nowUtc;
        public DateTime NowUtc => NowUtcValue;
    }
}
