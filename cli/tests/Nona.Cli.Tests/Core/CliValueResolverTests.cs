using static Nona.Cli.Tests.TestHelpers;

namespace Nona.Cli.Tests.Core;

public sealed class CliValueResolverTests
{
    private static readonly SemaphoreSlim EnvironmentLock = new(1, 1);

    [Test]
    public async Task BaseUrl_UsesSavedDefault_WhenNotProvided()
    {
        var resolver = new CliValueResolver(new CliDefaults { BaseUrl = "http://saved.internal:18080" });
        await Assert.That(resolver.BaseUrl(null)).IsEqualTo("http://saved.internal:18080");
    }

    [Test]
    public async Task BaseUrl_UsesEnvironmentVariable_OverSavedDefault()
    {
        await EnvironmentLock.WaitAsync();
        try
        {
            using var env = new EnvironmentScope(new Dictionary<string, string?> { ["NONA_CLI_BASE_URL"] = "http://env.internal:18080" });
            var resolver = new CliValueResolver(new CliDefaults { BaseUrl = "http://saved.internal:18080" });
            await Assert.That(resolver.BaseUrl(null)).IsEqualTo("http://env.internal:18080");
        }
        finally { EnvironmentLock.Release(); }
    }

    [Test]
    public async Task Project_UsesSavedDefault_WhenNotProvided()
    {
        var resolver = new CliValueResolver(new CliDefaults { Project = "saved-project" });
        await Assert.That(resolver.Project(null)).IsEqualTo("saved-project");
    }

    [Test]
    public async Task Project_UsesEnvironmentVariable_OverSavedDefault()
    {
        await EnvironmentLock.WaitAsync();
        try
        {
            using var env = new EnvironmentScope(new Dictionary<string, string?> { ["NONA_CLI_PROJECT_NAME"] = "env-project" });
            var resolver = new CliValueResolver(new CliDefaults { Project = "saved-project" });
            await Assert.That(resolver.Project(null)).IsEqualTo("env-project");
        }
        finally { EnvironmentLock.Release(); }
    }

    [Test]
    public async Task ResolveConnection_UsesSavedSession_WhenNoTokenProvided()
    {
        await EnvironmentLock.WaitAsync();
        try
        {
            using var env = new EnvironmentScope(new Dictionary<string, string?> { ["NONA_CLI_BEARER_TOKEN"] = null });
            var session = new CliAuthSession
            {
                BaseUrl = "http://saved.internal:18080", Token = "saved-token",
                Username = "admin@example.com", Role = "Admin",
                ExpiresAt = DateTime.UtcNow.AddHours(1), SavedAtUtc = DateTime.UtcNow
            };
            var result = new CliValueResolver(CliDefaults.Empty, session)
                .ResolveConnection("http://saved.internal:18080", null);
            await Assert.That(result.Success).IsTrue();
            await Assert.That(result.Connection!.BearerToken).IsEqualTo("saved-token");
        }
        finally { EnvironmentLock.Release(); }
    }

    [Test]
    public async Task ResolveConnection_UsesEnvironmentBearerToken()
    {
        await EnvironmentLock.WaitAsync();
        try
        {
            using var env = new EnvironmentScope(new Dictionary<string, string?> { ["NONA_CLI_BEARER_TOKEN"] = "token-123" });
            var result = new CliValueResolver(CliDefaults.Empty)
                .ResolveConnection("http://nona.internal:18080", null);
            await Assert.That(result.Success).IsTrue();
            await Assert.That(result.Connection!.BearerToken).IsEqualTo("token-123");
        }
        finally { EnvironmentLock.Release(); }
    }

    [Test]
    public async Task ResolveConnection_UsesSavedDefaultBaseUrl()
    {
        await EnvironmentLock.WaitAsync();
        try
        {
            using var env = new EnvironmentScope(new Dictionary<string, string?>
                { ["NONA_CLI_BASE_URL"] = null, ["NONA_CLI_BEARER_TOKEN"] = "token-123" });
            var result = new CliValueResolver(new CliDefaults { BaseUrl = "http://saved.internal:18080" })
                .ResolveConnection(null, null);
            await Assert.That(result.Success).IsTrue();
            await Assert.That(result.Connection!.BaseUrl).IsEqualTo("http://saved.internal:18080");
        }
        finally { EnvironmentLock.Release(); }
    }

    [Test]
    public async Task ResolveConnection_Fails_WhenNoAuthAvailable()
    {
        await EnvironmentLock.WaitAsync();
        try
        {
            using var env = new EnvironmentScope(new Dictionary<string, string?> { ["NONA_CLI_BEARER_TOKEN"] = null });
            var result = new CliValueResolver(CliDefaults.Empty).ResolveConnection("http://nona.internal:18080", null);
            await Assert.That(result.Success).IsFalse();
        }
        finally { EnvironmentLock.Release(); }
    }

    [Test]
    public async Task ResolveConnection_Fails_WhenNoBaseUrlAvailable()
    {
        await EnvironmentLock.WaitAsync();
        try
        {
            using var env = new EnvironmentScope(new Dictionary<string, string?>
                { ["NONA_CLI_BASE_URL"] = null, ["NONA_CLI_BEARER_TOKEN"] = "token-123" });
            var result = new CliValueResolver(CliDefaults.Empty).ResolveConnection(null, null);
            await Assert.That(result.Success).IsFalse();
        }
        finally { EnvironmentLock.Release(); }
    }

    [Test]
    public async Task ResolveConnection_DoesNotUseExpiredSession()
    {
        await EnvironmentLock.WaitAsync();
        try
        {
            using var env = new EnvironmentScope(new Dictionary<string, string?> { ["NONA_CLI_BEARER_TOKEN"] = null });
            var expired = new CliAuthSession
            {
                BaseUrl = "http://saved.internal:18080", Token = "expired-token",
                Username = "admin", Role = "Admin",
                ExpiresAt = DateTime.UtcNow.AddHours(-1), SavedAtUtc = DateTime.UtcNow.AddDays(-2)
            };
            var result = new CliValueResolver(CliDefaults.Empty, expired)
                .ResolveConnection("http://saved.internal:18080", null);
            await Assert.That(result.Success).IsFalse();
        }
        finally { EnvironmentLock.Release(); }
    }

    [Test]
    public async Task BuildFirebaseArgs_IncludesAllProvidedValues()
    {
        var args = new CliValueResolver(CliDefaults.Empty).BuildFirebaseArgs(
            "nona.migration.json", false, "http://nona.internal:18080", "my-project", "token-123", null, null);
        var rendered = string.Join(' ', args);
        await Assert.That(rendered).Contains("--config nona.migration.json");
        await Assert.That(rendered).Contains("--base-url http://nona.internal:18080");
        await Assert.That(rendered).Contains("--token token-123");
        await Assert.That(rendered).Contains("--project my-project");
    }

    [Test]
    public async Task BuildFirebaseArgs_IncludesDryRunFlag()
    {
        var args = new CliValueResolver(CliDefaults.Empty)
            .BuildFirebaseArgs(null, true, "http://x.com", null, "t", null, null);
        await Assert.That(args).Contains("--dry-run");
    }

    [Test]
    public async Task NormalizeConfigSettingName_NormalizesAliases()
    {
        await Assert.That(CliValueResolver.NormalizeConfigSettingName("api-url")).IsEqualTo("base-url");
        await Assert.That(CliValueResolver.NormalizeConfigSettingName("base-url")).IsEqualTo("base-url");
        await Assert.That(CliValueResolver.NormalizeConfigSettingName("project-name")).IsEqualTo("project");
        await Assert.That(CliValueResolver.NormalizeConfigSettingName("project")).IsEqualTo("project");
        await Assert.That(CliValueResolver.NormalizeConfigSettingName("unknown")).IsNull();
    }
}
