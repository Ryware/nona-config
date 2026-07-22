using Nona.Application.Api.ConfigEntries.Queries;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Entities;
using Nona.Domain.Enums;
using Nona.Domain.Interfaces;
using NSubstitute;

namespace Nona.Application.Tests.Api;

public class GetAllConfigValuesQueryTests
{
    private const string ProjectName = "storefront";
    private const string EnvironmentName = "production";
    private const string ApiKey = "client-api-key";

    private IApiKeyRepository _apiKeyRepository = null!;
    private IEnvironmentRepository _environmentRepository = null!;
    private IConfigReleaseRepository _configReleaseRepository = null!;
    private IApiKeyService _apiKeyService = null!;

    [Before(Test)]
    public void Setup()
    {
        _apiKeyRepository = Substitute.For<IApiKeyRepository>();
        _environmentRepository = Substitute.For<IEnvironmentRepository>();
        _configReleaseRepository = Substitute.For<IConfigReleaseRepository>();
        _apiKeyService = Substitute.For<IApiKeyService>();
    }

    [Test]
    public async Task ClientKey_ReturnsClientVisibleEntriesAndExcludesServerEntries()
    {
        SetupApiKey(KeyScope.Frontend);
        SetupRelease();

        var result = await CreateHandler().Handle(
            new GetAllConfigValuesQuery(EnvironmentName),
            CancellationToken.None);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Values is not null).IsTrue();
        var values = result.Values!;
        await Assert.That(values.Count).IsEqualTo(2);
        await Assert.That(values.ContainsKey("client-only")).IsTrue();
        await Assert.That(values.ContainsKey("shared")).IsTrue();
        await Assert.That(values.ContainsKey("server-only")).IsFalse();
        await Assert.That(values["client-only"].ContentType).IsEqualTo("boolean");
        await _configReleaseRepository.Received(1).ListEntriesAsync(
            ProjectName,
            EnvironmentName,
            "1.0.0",
            KeyScope.Frontend,
            Arg.Any<CancellationToken>());
        await _configReleaseRepository.DidNotReceive().ListAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AllScopeKey_StillReturnsOnlyClientVisibleEntries()
    {
        SetupApiKey(KeyScope.All);
        SetupRelease();

        var result = await CreateHandler().Handle(
            new GetAllConfigValuesQuery(EnvironmentName),
            CancellationToken.None);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Values!.Count).IsEqualTo(2);
        await Assert.That(result.Values.ContainsKey("server-only")).IsFalse();
    }

    [Test]
    public async Task ServerOnlyKey_IsRejectedWithoutEnumeratingEnvironment()
    {
        SetupApiKey(KeyScope.Backend);

        var result = await CreateHandler().Handle(
            new GetAllConfigValuesQuery(EnvironmentName),
            CancellationToken.None);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Environment not found");
        await _environmentRepository.DidNotReceive().GetAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task InvalidKey_ReturnsAuthenticationError()
    {
        _apiKeyService.GetCurrentApiKey().Returns("unknown-key");
        _apiKeyRepository.GetByKeyAsync("unknown-key", Arg.Any<CancellationToken>())
            .Returns((ApiKeyAuthenticationResult?)null);

        var result = await CreateHandler().Handle(
            new GetAllConfigValuesQuery(EnvironmentName),
            CancellationToken.None);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Invalid API key");
    }

    [Test]
    public async Task EnvironmentBoundKey_CannotReadAnotherEnvironment()
    {
        SetupApiKey(KeyScope.Frontend, "staging");

        var result = await CreateHandler().Handle(
            new GetAllConfigValuesQuery(EnvironmentName),
            CancellationToken.None);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Environment not found");
    }

    [Test]
    public async Task MatchingEtag_ReturnsNotModifiedWithoutLoadingReleaseEntries()
    {
        SetupApiKey(KeyScope.Frontend);
        SetupRelease();

        var first = await CreateHandler().Handle(
            new GetAllConfigValuesQuery(EnvironmentName),
            CancellationToken.None);
        _configReleaseRepository.ClearReceivedCalls();

        var result = await CreateHandler().Handle(
            new GetAllConfigValuesQuery(EnvironmentName, IfNoneMatch: first.Etag),
            CancellationToken.None);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.NotModified).IsTrue();
        await Assert.That(result.Values is null).IsTrue();
        await Assert.That(result.Etag).IsEqualTo(first.Etag);
        await _configReleaseRepository.Received(1).GetMetadataAsync(
            ProjectName,
            EnvironmentName,
            "1.0.0",
            Arg.Any<CancellationToken>());
        await _configReleaseRepository.DidNotReceive().ListAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
        await _configReleaseRepository.DidNotReceive().ListEntriesAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<KeyScope>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ExactVersion_UsesTargetedMetadataLookup()
    {
        SetupApiKey(KeyScope.Frontend);
        SetupRelease();
        _configReleaseRepository.GetMetadataAsync(
                ProjectName,
                EnvironmentName,
                "2.3.4",
                Arg.Any<CancellationToken>())
            .Returns(CreateReleaseMetadata("2.3.4", 2, 3, 4));
        _configReleaseRepository.ListEntriesAsync(
                ProjectName,
                EnvironmentName,
                "2.3.4",
                KeyScope.Frontend,
                Arg.Any<CancellationToken>())
            .Returns([]);

        var result = await CreateHandler().Handle(
            new GetAllConfigValuesQuery(EnvironmentName, Version: "2.3.4"),
            CancellationToken.None);

        await Assert.That(result.Success).IsTrue();
        await _configReleaseRepository.Received(1).GetMetadataAsync(
            ProjectName,
            EnvironmentName,
            "2.3.4",
            Arg.Any<CancellationToken>());
        await _configReleaseRepository.DidNotReceive().ListAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task VersionLine_UsesTargetedLatestPatchMetadataLookup()
    {
        SetupApiKey(KeyScope.Frontend);
        SetupRelease();
        _configReleaseRepository.GetLatestPatchMetadataAsync(
                ProjectName,
                EnvironmentName,
                2,
                3,
                Arg.Any<CancellationToken>())
            .Returns(CreateReleaseMetadata("2.3.7", 2, 3, 7));
        _configReleaseRepository.ListEntriesAsync(
                ProjectName,
                EnvironmentName,
                "2.3.7",
                KeyScope.Frontend,
                Arg.Any<CancellationToken>())
            .Returns([]);

        var result = await CreateHandler().Handle(
            new GetAllConfigValuesQuery(EnvironmentName, Version: "2.3.x"),
            CancellationToken.None);

        await Assert.That(result.Success).IsTrue();
        await _configReleaseRepository.Received(1).GetLatestPatchMetadataAsync(
            ProjectName,
            EnvironmentName,
            2,
            3,
            Arg.Any<CancellationToken>());
        await _configReleaseRepository.Received(1).ListEntriesAsync(
            ProjectName,
            EnvironmentName,
            "2.3.7",
            KeyScope.Frontend,
            Arg.Any<CancellationToken>());
        await _configReleaseRepository.DidNotReceive().ListAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    private GetAllConfigValuesQueryHandler CreateHandler() => new(
        _apiKeyRepository,
        _environmentRepository,
        _configReleaseRepository,
        _apiKeyService);

    private void SetupApiKey(KeyScope scope, string? environment = EnvironmentName)
    {
        _apiKeyService.GetCurrentApiKey().Returns(ApiKey);
        _apiKeyRepository.GetByKeyAsync(ApiKey, Arg.Any<CancellationToken>())
            .Returns(new ApiKeyAuthenticationResult(
                new Project { Name = ProjectName },
                scope,
                environment));
    }

    private void SetupRelease()
    {
        _environmentRepository.GetAsync(ProjectName, EnvironmentName, Arg.Any<CancellationToken>())
            .Returns(new ProjectEnvironment
            {
                Project = ProjectName,
                Name = EnvironmentName,
                ActiveReleaseVersion = "1.0.0"
            });
        var entries = new List<ConfigReleaseEntry>
        {
            Entry("client-only", "true", "bool", KeyScope.Frontend),
            Entry("server-only", "secret", "text", KeyScope.Backend),
            Entry("shared", "42", "number", KeyScope.All)
        };
        _configReleaseRepository.GetMetadataAsync(
                ProjectName,
                EnvironmentName,
                "1.0.0",
                Arg.Any<CancellationToken>())
            .Returns(CreateReleaseMetadata("1.0.0", 1, 0, 0, entries.Count));
        // Return all scopes here so the handler's defense-in-depth filter is also covered.
        _configReleaseRepository.ListEntriesAsync(
                ProjectName,
                EnvironmentName,
                "1.0.0",
                KeyScope.Frontend,
                Arg.Any<CancellationToken>())
            .Returns(entries);
    }

    private static ConfigRelease CreateReleaseMetadata(
        string version,
        int major,
        int minor,
        int patch,
        int entryCount = 0) => new()
        {
            Project = ProjectName,
            Environment = EnvironmentName,
            Version = version,
            Major = major,
            Minor = minor,
            Patch = patch,
            EntryCount = entryCount
        };

    private static ConfigReleaseEntry Entry(
        string key,
        string value,
        string contentType,
        KeyScope scope) => new()
        {
            Project = ProjectName,
            Environment = EnvironmentName,
            ReleaseVersion = "1.0.0",
            Key = key,
            Value = value,
            ContentType = contentType,
            Scope = scope
        };
}
