using Nona.Application.Api.ConfigEntries.Queries;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Entities;
using Nona.Domain.Enums;
using Nona.Domain.Interfaces;
using NSubstitute;

namespace Nona.Application.Tests.Api;

public class GetConfigEntryValueQueryTests
{
    private const string ProjectName = "test-project";
    private const string EnvironmentName = "development";
    private const string ConfigKey = "test-key";
    private const string ConfigValue = "test-value";
    private const string BackendScopedApiKey = "backend-api-key-123";
    private const string FrontendScopedApiKey = "frontend-api-key-456";

    private IProjectRepository _projectRepository = null!;
    private IApiKeyRepository _apiKeyRepository = null!;
    private IEnvironmentRepository _environmentRepository = null!;
    private IConfigEntryRepository _configEntryRepository = null!;
    private IApiKeyService _apiKeyService = null!;

    [Before(Test)]
    public void Setup()
    {
        _projectRepository = Substitute.For<IProjectRepository>();
        _apiKeyRepository = Substitute.For<IApiKeyRepository>();
        _environmentRepository = Substitute.For<IEnvironmentRepository>();
        _configEntryRepository = Substitute.For<IConfigEntryRepository>();
        _apiKeyService = Substitute.For<IApiKeyService>();
    }

    #region API Key Validation Tests

    [Test]
    public async Task GetConfigEntryValue_WithNoApiKey_ReturnsError()
    {
        // Arrange
        _apiKeyService.GetCurrentApiKey().Returns((string?)null);

        var handler = CreateHandler();
        var query = new GetConfigEntryValueQuery(EnvironmentName, ConfigKey);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("API key is required");
    }

    [Test]
    public async Task GetConfigEntryValue_WithEmptyApiKey_ReturnsError()
    {
        // Arrange
        _apiKeyService.GetCurrentApiKey().Returns(string.Empty);

        var handler = CreateHandler();
        var query = new GetConfigEntryValueQuery(EnvironmentName, ConfigKey);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("API key is required");
    }

    [Test]
    public async Task GetConfigEntryValue_WithInvalidApiKey_ReturnsError()
    {
        // Arrange
        _apiKeyService.GetCurrentApiKey().Returns("invalid-api-key");
        _apiKeyRepository.GetByKeyAsync("invalid-api-key", Arg.Any<CancellationToken>())
            .Returns((ApiKeyAuthenticationResult?)null);

        var handler = CreateHandler();
        var query = new GetConfigEntryValueQuery(EnvironmentName, ConfigKey);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Invalid API key");
    }

    #endregion

    #region Managed API Key Tests

    [Test]
    public async Task GetConfigEntryValue_WithManagedApiKey_CanReadScopedEnvironment()
    {
        // Arrange
        const string managedApiKey = "managed-api-key";
        var project = new Project { Name = ProjectName };
        _apiKeyService.GetCurrentApiKey().Returns(managedApiKey);
        _apiKeyRepository.GetByKeyAsync(managedApiKey, Arg.Any<CancellationToken>())
            .Returns(new ApiKeyAuthenticationResult(project, KeyScope.Frontend, EnvironmentName));
        SetupEnvironmentExists();
        SetupConfigEntry(KeyScope.Frontend);

        var handler = CreateHandler();
        var query = new GetConfigEntryValueQuery(EnvironmentName, ConfigKey);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo(ConfigValue);
    }

    [Test]
    public async Task GetConfigEntryValue_WithManagedApiKey_CannotReadOtherEnvironment()
    {
        // Arrange
        const string managedApiKey = "managed-api-key";
        var project = new Project { Name = ProjectName };
        _apiKeyService.GetCurrentApiKey().Returns(managedApiKey);
        _apiKeyRepository.GetByKeyAsync(managedApiKey, Arg.Any<CancellationToken>())
            .Returns(new ApiKeyAuthenticationResult(project, KeyScope.Frontend, "staging"));

        var handler = CreateHandler();
        var query = new GetConfigEntryValueQuery(EnvironmentName, ConfigKey);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Environment not found");
        await _environmentRepository.DidNotReceive().ExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    #endregion

    #region Backend Scope Tests

    [Test]
    public async Task GetConfigEntryValue_WithBackendScopedApiKey_CanReadBackendScopedEntry()
    {
        // Arrange
        SetupValidBackendScopedApiKey();
        SetupEnvironmentExists();
        SetupConfigEntry(KeyScope.Backend);

        var handler = CreateHandler();
        var query = new GetConfigEntryValueQuery(EnvironmentName, ConfigKey);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo(ConfigValue);
    }

    [Test]
    public async Task GetConfigEntryValue_WithBackendScopedApiKey_CanReadAllScopedEntry()
    {
        // Arrange
        SetupValidBackendScopedApiKey();
        SetupEnvironmentExists();
        SetupConfigEntry(KeyScope.All);

        var handler = CreateHandler();
        var query = new GetConfigEntryValueQuery(EnvironmentName, ConfigKey);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo(ConfigValue);
    }

    [Test]
    public async Task GetConfigEntryValue_WithBackendScopedApiKey_CannotReadFrontendOnlyScopedEntry()
    {
        // Arrange
        SetupValidBackendScopedApiKey();
        SetupEnvironmentExists();
        SetupConfigEntry(KeyScope.Frontend);

        var handler = CreateHandler();
        var query = new GetConfigEntryValueQuery(EnvironmentName, ConfigKey);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Config entry not found");
    }

    #endregion

    #region Frontend Scope Tests

    [Test]
    public async Task GetConfigEntryValue_WithFrontendScopedApiKey_CanReadFrontendScopedEntry()
    {
        // Arrange
        SetupValidFrontendScopedApiKey();
        SetupEnvironmentExists();
        SetupConfigEntry(KeyScope.Frontend);

        var handler = CreateHandler();
        var query = new GetConfigEntryValueQuery(EnvironmentName, ConfigKey);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo(ConfigValue);
    }

    [Test]
    public async Task GetConfigEntryValue_WithFrontendScopedApiKey_CanReadAllScopedEntry()
    {
        // Arrange
        SetupValidFrontendScopedApiKey();
        SetupEnvironmentExists();
        SetupConfigEntry(KeyScope.All);

        var handler = CreateHandler();
        var query = new GetConfigEntryValueQuery(EnvironmentName, ConfigKey);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo(ConfigValue);
    }

    [Test]
    public async Task GetConfigEntryValue_WithFrontendScopedApiKey_CannotReadBackendOnlyScopedEntry()
    {
        // Arrange
        SetupValidFrontendScopedApiKey();
        SetupEnvironmentExists();
        SetupConfigEntry(KeyScope.Backend);

        var handler = CreateHandler();
        var query = new GetConfigEntryValueQuery(EnvironmentName, ConfigKey);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Config entry not found");
    }

    #endregion

    #region Environment and Config Entry Validation Tests

    [Test]
    public async Task GetConfigEntryValue_WithValidApiKey_EnvironmentNotFound_ReturnsError()
    {
        // Arrange
        SetupValidBackendScopedApiKey();
        _environmentRepository.ExistsAsync(ProjectName, EnvironmentName, Arg.Any<CancellationToken>())
            .Returns(false);

        var handler = CreateHandler();
        var query = new GetConfigEntryValueQuery(EnvironmentName, ConfigKey);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Environment not found");
    }

    [Test]
    public async Task GetConfigEntryValue_WithValidApiKey_ConfigEntryNotFound_ReturnsError()
    {
        // Arrange
        SetupValidBackendScopedApiKey();
        SetupEnvironmentExists();
        _configEntryRepository.GetAsync(ProjectName, EnvironmentName, ConfigKey, Arg.Any<CancellationToken>())
            .Returns((ConfigEntry?)null);

        var handler = CreateHandler();
        var query = new GetConfigEntryValueQuery(EnvironmentName, ConfigKey);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Config entry not found");
    }

    #endregion

    #region Content Type Tests

    [Test]
    public async Task GetConfigEntryValue_ReturnsContentType()
    {
        // Arrange
        SetupValidBackendScopedApiKey();
        SetupEnvironmentExists();
        _configEntryRepository.GetAsync(ProjectName, EnvironmentName, ConfigKey, Arg.Any<CancellationToken>())
            .Returns(new ConfigEntry
            {
                Project = ProjectName,
                Environment = EnvironmentName,
                Key = ConfigKey,
                Value = "{\"key\": \"value\"}",
                ContentType = "application/json",
                Scope = KeyScope.All
            });

        var handler = CreateHandler();
        var query = new GetConfigEntryValueQuery(EnvironmentName, ConfigKey);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.LogicalContentType).IsEqualTo("json");
    }

    #endregion

    #region Read-Only Access Verification

    [Test]
    public async Task GetConfigEntryValue_ApiKeyOnlyAllowsReading_NoWriteOperations()
    {
        // Arrange
        SetupValidBackendScopedApiKey();
        SetupEnvironmentExists();
        SetupConfigEntry(KeyScope.All);

        var handler = CreateHandler();
        var query = new GetConfigEntryValueQuery(EnvironmentName, ConfigKey);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert - Verify only read operations were performed
        await Assert.That(result.Success).IsTrue();

        // Verify no write operations were called on repositories
        await _configEntryRepository.DidNotReceive().AddAsync(Arg.Any<ConfigEntry>(), Arg.Any<CancellationToken>());
        await _configEntryRepository.DidNotReceive().UpdateAsync(Arg.Any<ConfigEntry>(), Arg.Any<CancellationToken>());
        await _configEntryRepository.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _projectRepository.DidNotReceive().AddAsync(Arg.Any<Project>(), Arg.Any<CancellationToken>());
        await _projectRepository.DidNotReceive().UpdateAsync(Arg.Any<Project>(), Arg.Any<CancellationToken>());
        await _projectRepository.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    #endregion

    #region Helper Methods

    private GetConfigEntryValueQueryHandler CreateHandler()
    {
        return new GetConfigEntryValueQueryHandler(
            _apiKeyRepository,
            _environmentRepository,
            _configEntryRepository,
            _apiKeyService);
    }

    private void SetupValidBackendScopedApiKey()
    {
        var project = new Project
        {
            Name = ProjectName
        };

        _apiKeyService.GetCurrentApiKey().Returns(BackendScopedApiKey);
        _apiKeyRepository.GetByKeyAsync(BackendScopedApiKey, Arg.Any<CancellationToken>())
            .Returns(new ApiKeyAuthenticationResult(project, KeyScope.Backend, null));
    }

    private void SetupValidFrontendScopedApiKey()
    {
        var project = new Project
        {
            Name = ProjectName
        };

        _apiKeyService.GetCurrentApiKey().Returns(FrontendScopedApiKey);
        _apiKeyRepository.GetByKeyAsync(FrontendScopedApiKey, Arg.Any<CancellationToken>())
            .Returns(new ApiKeyAuthenticationResult(project, KeyScope.Frontend, null));
    }

    private void SetupEnvironmentExists()
    {
        _environmentRepository.ExistsAsync(ProjectName, EnvironmentName, Arg.Any<CancellationToken>())
            .Returns(true);
    }

    private void SetupConfigEntry(KeyScope scope)
    {
        _configEntryRepository.GetAsync(ProjectName, EnvironmentName, ConfigKey, Arg.Any<CancellationToken>())
            .Returns(new ConfigEntry
            {
                Project = ProjectName,
                Environment = EnvironmentName,
                Key = ConfigKey,
                Value = ConfigValue,
                ContentType = "text",
                Scope = scope
            });
    }

    #endregion
}
