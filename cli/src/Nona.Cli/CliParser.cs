namespace Nona.Cli;

internal static class CliParser
{
    public static CliParseResult Parse(string[] args)
    {
        if (args.Length == 0 || IsHelpToken(args[0]) || args.Any(static arg => arg is "--help" or "-h"))
            return CliParseResult.Help();

        if (IsToken(args[0], "keys"))
            return ParseKeys(args.Skip(1).ToArray());

        if (IsToken(args[0], "migrate"))
            return ParseMigrate(args.Skip(1).ToArray());

        return CliParseResult.Fail($"Unknown command '{args[0]}'.");
    }

    private static CliParseResult ParseKeys(string[] args)
    {
        if (args.Length == 0)
            return CliParseResult.Fail("Missing keys subcommand. Use 'show' or 'reroll'.");

        var subcommand = args[0];
        var parsedArguments = CommandArguments.Parse(args.Skip(1).ToArray());
        if (!parsedArguments.Success)
            return CliParseResult.Fail(parsedArguments.Error!);

        var connectionResult = ResolveConnection(parsedArguments);
        if (!connectionResult.Success)
            return CliParseResult.Fail(connectionResult.Error!);

        var project = GetOptionOrEnvironment(parsedArguments, "project", "NONA_CLI_PROJECT_NAME");
        if (string.IsNullOrWhiteSpace(project))
            return CliParseResult.Fail("Keys commands require --project or NONA_CLI_PROJECT_NAME.");

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

    private static CliParseResult ParseMigrate(string[] args)
    {
        if (args.Length == 0)
            return CliParseResult.Fail("Missing migration source. Only 'firebase' is currently supported.");

        if (!IsToken(args[0], "firebase"))
            return CliParseResult.Fail($"Unknown migration source '{args[0]}'.");

        var forwardedArguments = args.Skip(1).ToList();
        AppendEnvironmentOverride(forwardedArguments, "base-url", "NONA_CLI_BASE_URL", "api-url");
        AppendEnvironmentOverride(forwardedArguments, "project", "NONA_CLI_PROJECT_NAME", "project-name");
        AppendEnvironmentOverride(forwardedArguments, "email", "NONA_CLI_EMAIL");
        AppendEnvironmentOverride(forwardedArguments, "password", "NONA_CLI_PASSWORD");
        AppendEnvironmentOverride(forwardedArguments, "token", "NONA_CLI_BEARER_TOKEN", "bearer-token");

        return CliParseResult.Run(new MigrateFirebaseCommand(forwardedArguments.ToArray()));
    }

    private static ConnectionResolutionResult ResolveConnection(CommandArguments arguments)
    {
        var baseUrl = GetOptionOrEnvironment(arguments, "base-url", "NONA_CLI_BASE_URL");
        if (string.IsNullOrWhiteSpace(baseUrl))
            return ConnectionResolutionResult.Fail("Set --base-url/--api-url or NONA_CLI_BASE_URL.");

        var bearerToken = GetOptionOrEnvironment(arguments, "token", "NONA_CLI_BEARER_TOKEN");
        var email = GetOptionOrEnvironment(arguments, "email", "NONA_CLI_EMAIL");
        var password = GetOptionOrEnvironment(arguments, "password", "NONA_CLI_PASSWORD");

        var hasBearerToken = !string.IsNullOrWhiteSpace(bearerToken);
        var hasEmailPassword = !string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(password);
        if (!hasBearerToken && !hasEmailPassword)
            return ConnectionResolutionResult.Fail("Set --token or NONA_CLI_BEARER_TOKEN, or provide --email and --password.");

        return ConnectionResolutionResult.Ok(new NonaCliConnectionOptions(baseUrl, email, password, bearerToken));
    }

    private static void AppendEnvironmentOverride(List<string> arguments, string optionName, string environmentVariable, params string[] aliases)
    {
        if (HasOption(arguments, optionName, aliases))
            return;

        var value = Environment.GetEnvironmentVariable(environmentVariable);
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

    private static string? GetOptionOrEnvironment(CommandArguments arguments, string optionName, string environmentVariable)
    {
        var value = GetOption(arguments, optionName);
        return string.IsNullOrWhiteSpace(value)
            ? Environment.GetEnvironmentVariable(environmentVariable)
            : value;
    }

    private static string? GetOption(CommandArguments arguments, string optionName)
    {
        return arguments.Options.TryGetValue(optionName, out var value) ? value : null;
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
