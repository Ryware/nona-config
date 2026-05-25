namespace Nona.Cli;

internal static class CliParser
{
    public static CliParseResult Parse(string[] args, CliDefaults? defaults = null, CliAuthSession? session = null)
    {
        defaults ??= CliDefaults.Empty;

        if (args.Length == 0 || IsHelpToken(args[0]) || args.Any(static arg => arg is "--help" or "-h"))
            return CliParseResult.Help();

        if (IsToken(args[0], "auth"))
            return ParseAuth(args.Skip(1).ToArray(), defaults, session);

        if (IsToken(args[0], "config"))
            return ParseConfig(args.Skip(1).ToArray(), defaults);

        if (IsToken(args[0], "keys"))
            return ParseKeys(args.Skip(1).ToArray(), defaults, session);

        if (IsToken(args[0], "migrate"))
            return ParseMigrate(args.Skip(1).ToArray(), defaults, session);

        return CliParseResult.Fail($"Unknown command '{args[0]}'.");
    }

    private static CliParseResult ParseAuth(string[] args, CliDefaults defaults, CliAuthSession? session)
    {
        if (args.Length == 0)
            return CliParseResult.Fail("Missing auth subcommand. Use 'login', 'logout', or 'whoami'.");

        if (IsToken(args[0], "login"))
        {
            var parsedArguments = CommandArguments.Parse(args.Skip(1).ToArray());
            if (!parsedArguments.Success)
                return CliParseResult.Fail(parsedArguments.Error!);

            var baseUrl = GetOptionEnvironmentOrDefault(parsedArguments, "base-url", "NONA_CLI_BASE_URL", defaults.BaseUrl);
            if (string.IsNullOrWhiteSpace(baseUrl))
                return CliParseResult.Fail("Auth login requires --base-url, NONA_CLI_BASE_URL, or a saved default base-url.");

            var email = GetOptionEnvironmentOrDefault(parsedArguments, "email", "NONA_CLI_EMAIL", null);
            var password = GetOptionEnvironmentOrDefault(parsedArguments, "password", "NONA_CLI_PASSWORD", null);

            return CliParseResult.Run(new LoginCommand(baseUrl, email, password));
        }

        if (IsToken(args[0], "logout"))
            return CliParseResult.Run(new LogoutCommand());

        if (IsToken(args[0], "whoami"))
            return CliParseResult.Run(new WhoAmICommand(session));

        return CliParseResult.Fail($"Unknown auth subcommand '{args[0]}'.");
    }

    private static CliParseResult ParseConfig(string[] args, CliDefaults defaults)
    {
        if (args.Length == 0 || IsToken(args[0], "show"))
            return CliParseResult.Run(new ShowCliDefaultsCommand(defaults));

        if (IsToken(args[0], "set") || IsToken(args[0], "set-default"))
        {
            if (args.Length < 3)
                return CliParseResult.Fail("Usage: nona config set <base-url|project> <value>.");

            var settingName = NormalizeConfigSettingName(args[1]);
            if (settingName is null)
                return CliParseResult.Fail("Supported default settings are 'base-url' and 'project'.");

            var value = string.Join(' ', args.Skip(2)).Trim();
            if (string.IsNullOrWhiteSpace(value))
                return CliParseResult.Fail($"Default '{settingName}' requires a non-empty value.");

            return CliParseResult.Run(new SetCliDefaultCommand(settingName, value, defaults));
        }

        return CliParseResult.Fail($"Unknown config subcommand '{args[0]}'.");
    }

    private static CliParseResult ParseKeys(string[] args, CliDefaults defaults, CliAuthSession? session)
    {
        if (args.Length == 0)
            return CliParseResult.Fail("Missing keys subcommand. Use 'show' or 'reroll'.");

        var subcommand = args[0];
        var parsedArguments = CommandArguments.Parse(args.Skip(1).ToArray());
        if (!parsedArguments.Success)
            return CliParseResult.Fail(parsedArguments.Error!);

        var connectionResult = ResolveConnection(parsedArguments, defaults, session);
        if (!connectionResult.Success)
            return CliParseResult.Fail(connectionResult.Error!);

        var project = GetOptionEnvironmentOrDefault(parsedArguments, "project", "NONA_CLI_PROJECT_NAME", defaults.Project);
        if (string.IsNullOrWhiteSpace(project))
            return CliParseResult.Fail("Keys commands require --project, NONA_CLI_PROJECT_NAME, or a saved default project.");

        if (IsToken(subcommand, "show"))
            return CliParseResult.Run(new ShowKeysCommand(connectionResult.Connection!, project));

        if (IsToken(subcommand, "reroll"))
        {
            var keyType = GetOption(parsedArguments, "type");
            if (string.IsNullOrWhiteSpace(keyType))
                return CliParseResult.Fail("Key reroll requires --type server|client|both.");

            if (!IsSupportedKeyType(keyType))
                return CliParseResult.Fail("Key reroll type must be server, client, or both.");

            return CliParseResult.Run(new RerollKeysCommand(connectionResult.Connection!, project, keyType.ToLowerInvariant()));
        }

        return CliParseResult.Fail($"Unknown keys subcommand '{subcommand}'.");
    }

    private static CliParseResult ParseMigrate(string[] args, CliDefaults defaults, CliAuthSession? session)
    {
        if (args.Length == 0)
            return CliParseResult.Fail("Missing migration source. Only 'firebase' is currently supported.");

        if (!IsToken(args[0], "firebase"))
            return CliParseResult.Fail($"Unknown migration source '{args[0]}'.");

        var forwardedArguments = args.Skip(1).ToList();
        AppendResolvedValue(forwardedArguments, "base-url", "NONA_CLI_BASE_URL", defaults.BaseUrl, "api-url");
        AppendResolvedValue(forwardedArguments, "project", "NONA_CLI_PROJECT_NAME", defaults.Project, "project-name");
        AppendResolvedValue(forwardedArguments, "email", "NONA_CLI_EMAIL", null);
        AppendResolvedValue(forwardedArguments, "password", "NONA_CLI_PASSWORD", null);
        AppendResolvedValue(forwardedArguments, "token", "NONA_CLI_BEARER_TOKEN", null, "bearer-token");

        var resolvedBaseUrl = ResolveForwardedOption(forwardedArguments, "base-url");
        var hasToken = ResolveForwardedOption(forwardedArguments, "token") is not null;
        var hasEmail = ResolveForwardedOption(forwardedArguments, "email") is not null;
        var hasPassword = ResolveForwardedOption(forwardedArguments, "password") is not null;

        if (!hasToken && !(hasEmail && hasPassword) && session is not null && resolvedBaseUrl is not null && !session.IsExpired && session.MatchesBaseUrl(resolvedBaseUrl))
        {
            forwardedArguments.Add("--token");
            forwardedArguments.Add(session.Token);
        }

        return CliParseResult.Run(new MigrateFirebaseCommand(forwardedArguments.ToArray()));
    }

    private static ConnectionResolutionResult ResolveConnection(CommandArguments arguments, CliDefaults defaults, CliAuthSession? session)
    {
        var baseUrl = GetOptionEnvironmentOrDefault(arguments, "base-url", "NONA_CLI_BASE_URL", defaults.BaseUrl);
        if (string.IsNullOrWhiteSpace(baseUrl))
            return ConnectionResolutionResult.Fail("Set --base-url/--api-url, NONA_CLI_BASE_URL, or a saved default base-url.");

        var bearerToken = GetOptionEnvironmentOrDefault(arguments, "token", "NONA_CLI_BEARER_TOKEN", null);
        var email = GetOptionEnvironmentOrDefault(arguments, "email", "NONA_CLI_EMAIL", null);
        var password = GetOptionEnvironmentOrDefault(arguments, "password", "NONA_CLI_PASSWORD", null);

        var hasBearerToken = !string.IsNullOrWhiteSpace(bearerToken);
        var hasEmailPassword = !string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(password);
        if (!hasBearerToken && !hasEmailPassword)
        {
            if (session is not null && !session.IsExpired && session.MatchesBaseUrl(baseUrl))
            {
                bearerToken = session.Token;
                hasBearerToken = true;
            }
            else
            {
                return ConnectionResolutionResult.Fail("Set --token, NONA_CLI_BEARER_TOKEN, provide --email and --password, or run `nona auth login`.");
            }
        }

        return ConnectionResolutionResult.Ok(new NonaCliConnectionOptions(baseUrl, email, password, bearerToken));
    }

    private static void AppendResolvedValue(
        List<string> arguments,
        string optionName,
        string environmentVariable,
        string? defaultValue,
        params string[] aliases)
    {
        if (HasOption(arguments, optionName, aliases))
            return;

        var value = Environment.GetEnvironmentVariable(environmentVariable);
        if (string.IsNullOrWhiteSpace(value))
            value = defaultValue;

        if (string.IsNullOrWhiteSpace(value))
            return;

        arguments.Add($"--{optionName}");
        arguments.Add(value);
    }

    private static bool HasOption(IReadOnlyList<string> arguments, string optionName, params string[] aliases)
    {
        var names = new[] { optionName }.Concat(aliases).ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < arguments.Count; index++)
        {
            var token = arguments[index];
            if (!token.StartsWith("--", StringComparison.Ordinal))
                continue;

            var normalized = NormalizeOptionName(token);
            if (normalized is not null && names.Contains(normalized))
                return true;
        }

        return false;
    }

    private static string? GetOptionEnvironmentOrDefault(
        CommandArguments arguments,
        string optionName,
        string environmentVariable,
        string? defaultValue)
    {
        var value = GetOption(arguments, optionName);
        if (!string.IsNullOrWhiteSpace(value))
            return value;

        value = Environment.GetEnvironmentVariable(environmentVariable);
        return string.IsNullOrWhiteSpace(value)
            ? defaultValue
            : value;
    }

    private static string? GetOption(CommandArguments arguments, string optionName)
    {
        return arguments.Options.TryGetValue(optionName, out var value) ? value : null;
    }

    private static string? ResolveForwardedOption(IReadOnlyList<string> arguments, string optionName)
    {
        for (var index = 0; index < arguments.Count - 1; index++)
        {
            var normalized = NormalizeOptionName(arguments[index]);
            if (string.Equals(normalized, optionName, StringComparison.OrdinalIgnoreCase))
                return arguments[index + 1];
        }

        return null;
    }

    private static bool IsSupportedKeyType(string keyType)
    {
        return IsToken(keyType, "server")
            || IsToken(keyType, "client")
            || IsToken(keyType, "both");
    }

    private static bool IsToken(string value, string expected)
    {
        return string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsHelpToken(string value)
    {
        return IsToken(value, "help")
            || IsToken(value, "--help")
            || IsToken(value, "-h");
    }

    internal static string? NormalizeOptionName(string token)
    {
        if (!token.StartsWith("--", StringComparison.Ordinal))
            return null;

        var rawName = token[2..];
        return rawName.ToLowerInvariant() switch
        {
            "api-url" => "base-url",
            "project-name" => "project",
            "bearer-token" => "token",
            "key-type" => "type",
            _ => rawName.ToLowerInvariant()
        };
    }

    private static string? NormalizeConfigSettingName(string token)
    {
        return token.ToLowerInvariant() switch
        {
            "base-url" or "api-url" => "base-url",
            "project" or "project-name" => "project",
            _ => null
        };
    }
}

internal sealed record CliParseResult(bool Success, bool ShowHelp, string? Error, CliCommand? Command)
{
    public static CliParseResult Help() => new(true, true, null, null);

    public static CliParseResult Run(CliCommand command) => new(true, false, null, command);

    public static CliParseResult Fail(string error) => new(false, false, error, null);
}

internal sealed class CommandArguments
{
    private CommandArguments(bool success, string? error, Dictionary<string, string?> options)
    {
        Success = success;
        Error = error;
        Options = options;
    }

    public bool Success { get; }

    public string? Error { get; }

    public IReadOnlyDictionary<string, string?> Options { get; }

    public static CommandArguments Parse(string[] args)
    {
        var options = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < args.Length; index++)
        {
            var token = args[index];
            var optionName = CliParser.NormalizeOptionName(token);
            if (optionName is null)
                return new CommandArguments(false, $"Unexpected positional argument '{token}'.", options);

            if (string.Equals(optionName, "dry-run", StringComparison.OrdinalIgnoreCase))
            {
                options[optionName] = "true";
                continue;
            }

            if (index + 1 >= args.Length)
                return new CommandArguments(false, $"Option '{token}' requires a value.", options);

            options[optionName] = args[++index];
        }

        return new CommandArguments(true, null, options);
    }
}

internal sealed record ConnectionResolutionResult(bool Success, string? Error, NonaCliConnectionOptions? Connection)
{
    public static ConnectionResolutionResult Ok(NonaCliConnectionOptions connection) => new(true, null, connection);

    public static ConnectionResolutionResult Fail(string error) => new(false, error, null);
}
