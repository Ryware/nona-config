namespace Nona.Cli.Tests;

public sealed class CliParserTests
{
    private static readonly SemaphoreSlim EnvironmentLock = new(1, 1);

    [Test]
    public async Task Parse_KeysShow_UsesEnvironmentFallbacks()
    {
        await EnvironmentLock.WaitAsync();
        try
        {
            using var environmentScope = new EnvironmentScope(new Dictionary<string, string?>
            {
                ["NONA_CLI_BASE_URL"] = "http://nona.internal:18080",
                ["NONA_CLI_PROJECT_NAME"] = "mobile-app",
                ["NONA_CLI_BEARER_TOKEN"] = "token-123"
            });

            var result = CliParser.Parse(["keys", "show"]);

            await Assert.That(result.Success).IsTrue();
            await Assert.That(result.Command).IsTypeOf<ShowKeysCommand>();

            var command = (ShowKeysCommand)result.Command!;
            await Assert.That(command.Project).IsEqualTo("mobile-app");
            await Assert.That(command.Connection.BaseUrl).IsEqualTo("http://nona.internal:18080");
            await Assert.That(command.Connection.BearerToken).IsEqualTo("token-123");
        }
        finally
        {
            EnvironmentLock.Release();
        }
    }

    [Test]
    public async Task Parse_KeysReroll_RejectsUnknownKeyType()
    {
        await EnvironmentLock.WaitAsync();
        try
        {
            var result = CliParser.Parse(
            [
                "keys",
                "reroll",
                "--project", "mobile-app",
                "--base-url", "https://nona.example.com",
                "--token", "token-123",
                "--type", "rotate-all"
            ]);

            await Assert.That(result.Success).IsFalse();
            await Assert.That(result.Error).IsEqualTo("Key reroll type must be server, client, or both.");
        }
        finally
        {
            EnvironmentLock.Release();
        }
    }

    [Test]
    public async Task Parse_MigrateFirebase_AppendsCliEnvironmentOverrides()
    {
        await EnvironmentLock.WaitAsync();
        try
        {
            using var environmentScope = new EnvironmentScope(new Dictionary<string, string?>
            {
                ["NONA_CLI_BASE_URL"] = "http://nona.internal:18080",
                ["NONA_CLI_BEARER_TOKEN"] = "token-123",
                ["NONA_CLI_PROJECT_NAME"] = null,
                ["NONA_CLI_EMAIL"] = null,
                ["NONA_CLI_PASSWORD"] = null
            });

            var result = CliParser.Parse(["migrate", "firebase", "--config", "nona.migration.json"]);

            await Assert.That(result.Success).IsTrue();
            await Assert.That(result.Command).IsTypeOf<MigrateFirebaseCommand>();

            var command = (MigrateFirebaseCommand)result.Command!;
            var renderedArguments = string.Join(' ', command.Arguments);
            await Assert.That(renderedArguments).Contains("--config nona.migration.json");
            await Assert.That(renderedArguments).Contains("--base-url http://nona.internal:18080");
            await Assert.That(renderedArguments).Contains("--token token-123");
        }
        finally
        {
            EnvironmentLock.Release();
        }
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
