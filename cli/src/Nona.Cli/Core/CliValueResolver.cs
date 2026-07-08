namespace Nona.Cli;

internal sealed class CliValueResolver(CliDefaults defaults, CliAuthSession? session = null)
{
    public string? BaseUrl(string? parsed)
        => Resolve(parsed, "NONA_CLI_BASE_URL", defaults.BaseUrl);

    public string? Project(string? parsed)
        => Resolve(parsed, "NONA_CLI_PROJECT_NAME", defaults.Project);

    public string? Token(string? parsed)
        => Resolve(parsed, "NONA_CLI_BEARER_TOKEN", null);

    public string? Email(string? parsed)
        => Resolve(parsed, "NONA_CLI_EMAIL", null);

    public string? Password(string? parsed)
        => Resolve(parsed, "NONA_CLI_PASSWORD", null);

    public ConnectionResolutionResult ResolveConnection(
        string? parsedBaseUrl,
        string? parsedToken)
    {
        var baseUrl = BaseUrl(parsedBaseUrl);
        var token = Token(parsedToken);

        if (string.IsNullOrWhiteSpace(baseUrl) && session is not null && !session.IsExpired)
            baseUrl = session.BaseUrl;

        if (string.IsNullOrWhiteSpace(baseUrl))
            return ConnectionResolutionResult.Fail("Set --base-url/--api-url, NONA_CLI_BASE_URL, or a saved default base-url.");

        var hasToken = !string.IsNullOrWhiteSpace(token);

        if (!hasToken)
        {
            if (session is not null && !session.IsExpired && session.MatchesBaseUrl(baseUrl))
                token = session.Token;
            else
                return ConnectionResolutionResult.Fail("Set --token, NONA_CLI_BEARER_TOKEN, or run `nona auth login`.");
        }

        return ConnectionResolutionResult.Ok(new NonaCliConnectionOptions(baseUrl, token));
    }

    public string[] BuildFirebaseArgs(
        string? config,
        bool dryRun,
        string? baseUrl,
        string? project,
        string? token,
        string? email,
        string? password)
    {
        var args = new List<string>();
        if (config is not null) { args.Add("--config"); args.Add(config); }
        if (dryRun) args.Add("--dry-run");
        if (baseUrl is not null) { args.Add("--base-url"); args.Add(baseUrl); }
        if (project is not null) { args.Add("--project"); args.Add(project); }
        if (token is not null) { args.Add("--token"); args.Add(token); }
        if (email is not null) { args.Add("--email"); args.Add(email); }
        if (password is not null) { args.Add("--password"); args.Add(password); }
        return [.. args];
    }

    public static string? NormalizeConfigSettingName(string name) =>
        name.ToLowerInvariant() switch
        {
            "base-url" or "api-url" => "base-url",
            "project" or "project-name" => "project",
            _ => null
        };

    private static string? Resolve(string? parsed, string envVar, string? defaultValue)
    {
        if (!string.IsNullOrWhiteSpace(parsed))
            return parsed;

        var envValue = Environment.GetEnvironmentVariable(envVar);
        return string.IsNullOrWhiteSpace(envValue) ? defaultValue : envValue;
    }
}

internal sealed record ConnectionResolutionResult(bool Success, string? Error, NonaCliConnectionOptions? Connection)
{
    public static ConnectionResolutionResult Ok(NonaCliConnectionOptions connection) => new(true, null, connection);
    public static ConnectionResolutionResult Fail(string error) => new(false, error, null);
}
