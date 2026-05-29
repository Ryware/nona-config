namespace Nona.Cli.Tests;

public sealed class CliValueResolverTests
{
    private static readonly SemaphoreSlim EnvironmentLock = new(1, 1);

    [Test]
    public async Task BaseUrl_UsesSavedDefault_WhenNotProvided()
    {
        var defaults = new CliDefaults { BaseUrl = "http://saved.internal:18080" };
        var resolver = new CliValueResolver(defaults);

        var result = resolver.BaseUrl(null);

        await Assert.That(result).IsEqualTo("http://saved.internal:18080");
    }

    [Test]
    public async Task BaseUrl_UsesEnvironmentVariable_OverSavedDefault()
    {
        await EnvironmentLock.WaitAsync();
        try
        {
            using var env = new EnvironmentScope(new Dictionary<string, string?>
            {
                ["NONA_CLI_BASE_URL"] = "http://env.internal:18080"
            });

            var defaults = new CliDefaults { BaseUrl = "http://saved.internal:18080" };
            var resolver = new CliValueResolver(defaults);

            var result = resolver.BaseUrl(null);

            await Assert.That(result).IsEqualTo("http://env.internal:18080");
        }
        finally
        {
            EnvironmentLock.Release();
        }
    }

    [Test]
    public async Task Project_UsesSavedDefault_WhenNotProvided()
    {
        var defaults = new CliDefaults { Project = "saved-project" };
        var resolver = new CliValueResolver(defaults);

        var result = resolver.Project(null);

        await Assert.That(result).IsEqualTo("saved-project");
    }

    [Test]
    public async Task Project_UsesEnvironmentVariable_OverSavedDefault()
    {
        await EnvironmentLock.WaitAsync();
        try
        {
            using var env = new EnvironmentScope(new Dictionary<string, string?>
            {
                ["NONA_CLI_PROJECT_NAME"] = "env-project"
            });

            var defaults = new CliDefaults { Project = "saved-project" };
            var resolver = new CliValueResolver(defaults);

            var result = resolver.Project(null);

            await Assert.That(result).IsEqualTo("env-project");
        }
        finally
        {
            EnvironmentLock.Release();
        }
    }

    [Test]
    public async Task ResolveConnection_UsesSavedSession_WhenNoAuthProvided()
    {
        await EnvironmentLock.WaitAsync();
        try
        {
            using var env = new EnvironmentScope(new Dictionary<string, string?>
            {
                ["NONA_CLI_BEARER_TOKEN"] = null,
                ["NONA_CLI_EMAIL"] = null,
                ["NONA_CLI_PASSWORD"] = null
            });

            var session = new CliAuthSession
            {
                BaseUrl = "http://saved.internal:18080",
                Token = "saved-token",
                Username = "admin@example.com",
                Role = "Admin",
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                SavedAtUtc = DateTime.UtcNow
            };

            var resolver = new CliValueResolver(CliDefaults.Empty, session);

            var result = resolver.ResolveConnection("http://saved.internal:18080", null, null, null);

            await Assert.That(result.Success).IsTrue();
            await Assert.That(result.Connection!.BearerToken).IsEqualTo("saved-token");
        }
        finally
        {
            EnvironmentLock.Release();
        }
    }

    [Test]
    public async Task ResolveConnection_UsesEnvironmentBearerToken()
    {
        await EnvironmentLock.WaitAsync();
        try
        {
            using var env = new EnvironmentScope(new Dictionary<string, string?>
            {
                ["NONA_CLI_BEARER_TOKEN"] = "token-123",
                ["NONA_CLI_EMAIL"] = null,
                ["NONA_CLI_PASSWORD"] = null
            });

            var resolver = new CliValueResolver(CliDefaults.Empty);

            var result = resolver.ResolveConnection("http://nona.internal:18080", null, null, null);

            await Assert.That(result.Success).IsTrue();
            await Assert.That(result.Connection!.BearerToken).IsEqualTo("token-123");
        }
        finally
        {
            EnvironmentLock.Release();
        }
    }

    [Test]
    public async Task ResolveConnection_UsesSavedDefaultsForBaseUrl()
    {
        await EnvironmentLock.WaitAsync();
        try
        {
            using var env = new EnvironmentScope(new Dictionary<string, string?>
            {
                ["NONA_CLI_BASE_URL"] = null,
                ["NONA_CLI_BEARER_TOKEN"] = "token-123"
            });

            var defaults = new CliDefaults { BaseUrl = "http://saved.internal:18080" };
            var resolver = new CliValueResolver(defaults);

            var result = resolver.ResolveConnection(null, null, null, null);

            await Assert.That(result.Success).IsTrue();
            await Assert.That(result.Connection!.BaseUrl).IsEqualTo("http://saved.internal:18080");
        }
        finally
        {
            EnvironmentLock.Release();
        }
    }

    [Test]
    public async Task ResolveConnection_Fails_WhenNoAuthIsAvailable()
    {
        await EnvironmentLock.WaitAsync();
        try
        {
            using var env = new EnvironmentScope(new Dictionary<string, string?>
            {
                ["NONA_CLI_BEARER_TOKEN"] = null,
                ["NONA_CLI_EMAIL"] = null,
                ["NONA_CLI_PASSWORD"] = null
            });

            var resolver = new CliValueResolver(CliDefaults.Empty);

            var result = resolver.ResolveConnection("http://nona.internal:18080", null, null, null);

            await Assert.That(result.Success).IsFalse();
        }
        finally
        {
            EnvironmentLock.Release();
        }
    }

    [Test]
    public async Task BuildFirebaseArgs_IncludesResolvedValues()
    {
        var resolver = new CliValueResolver(CliDefaults.Empty);

        var args = resolver.BuildFirebaseArgs(
            config: "nona.migration.json",
            dryRun: false,
            baseUrl: "http://nona.internal:18080",
            project: "my-project",
            token: "token-123",
            email: null,
            password: null);

        var rendered = string.Join(' ', args);
        await Assert.That(rendered).Contains("--config nona.migration.json");
        await Assert.That(rendered).Contains("--base-url http://nona.internal:18080");
        await Assert.That(rendered).Contains("--token token-123");
        await Assert.That(rendered).Contains("--project my-project");
    }

    [Test]
    public async Task BuildFirebaseArgs_WithEnvironmentOverrides()
    {
        await EnvironmentLock.WaitAsync();
        try
        {
            using var env = new EnvironmentScope(new Dictionary<string, string?>
            {
                ["NONA_CLI_BASE_URL"] = "http://nona.internal:18080",
                ["NONA_CLI_BEARER_TOKEN"] = "token-123",
                ["NONA_CLI_PROJECT_NAME"] = null,
                ["NONA_CLI_EMAIL"] = null,
                ["NONA_CLI_PASSWORD"] = null
            });

            var resolver = new CliValueResolver(CliDefaults.Empty);

            var args = resolver.BuildFirebaseArgs(
                config: "nona.migration.json",
                dryRun: false,
                baseUrl: resolver.BaseUrl(null),
                project: resolver.Project(null),
                token: resolver.Token(null),
                email: resolver.Email(null),
                password: resolver.Password(null));

            var rendered = string.Join(' ', args);
            await Assert.That(rendered).Contains("--config nona.migration.json");
            await Assert.That(rendered).Contains("--base-url http://nona.internal:18080");
            await Assert.That(rendered).Contains("--token token-123");
        }
        finally
        {
            EnvironmentLock.Release();
        }
    }

    [Test]
    public async Task BuildFirebaseArgs_WithSavedDefaults()
    {
        await EnvironmentLock.WaitAsync();
        try
        {
            using var env = new EnvironmentScope(new Dictionary<string, string?>
            {
                ["NONA_CLI_BASE_URL"] = null,
                ["NONA_CLI_PROJECT_NAME"] = null,
                ["NONA_CLI_BEARER_TOKEN"] = "token-123",
                ["NONA_CLI_EMAIL"] = null,
                ["NONA_CLI_PASSWORD"] = null
            });

            var defaults = new CliDefaults
            {
                BaseUrl = "http://saved.internal:18080",
                Project = "saved-project"
            };
            var resolver = new CliValueResolver(defaults);

            var args = resolver.BuildFirebaseArgs(
                config: "nona.migration.json",
                dryRun: false,
                baseUrl: resolver.BaseUrl(null),
                project: resolver.Project(null),
                token: resolver.Token(null),
                email: resolver.Email(null),
                password: resolver.Password(null));

            var rendered = string.Join(' ', args);
            await Assert.That(rendered).Contains("--base-url http://saved.internal:18080");
            await Assert.That(rendered).Contains("--project saved-project");
            await Assert.That(rendered).Contains("--token token-123");
        }
        finally
        {
            EnvironmentLock.Release();
        }
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

    private sealed class EnvironmentScope : IDisposable
    {
        private readonly Dictionary<string, string?> _previousValues = new(StringComparer.Ordinal);

        public EnvironmentScope(IReadOnlyDictionary<string, string?> values)
        {
            foreach (var (key, value) in values)
            {
                _previousValues[key] = Environment.GetEnvironmentVariable(key);
                Environment.SetEnvironmentVariable(key, value);
            }
        }

        public void Dispose()
        {
            foreach (var (key, value) in _previousValues)
                Environment.SetEnvironmentVariable(key, value);
        }
    }
}
