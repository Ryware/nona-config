using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using Nona.Cli.Init.Commands;

namespace Nona.Cli.Init;

internal sealed class InitCommands(CliContext ctx) : ICliCommandGroup
{
    private const string DefaultBaseUrl = "http://localhost:18080";
    private const string DefaultEnvironment = "production";
    private const string DefaultSeedFlag = "Features:Example=true";

    public Command Build()
    {
        var baseUrlOpt = new Option<string?>(["--base-url", "--api-url"],
            "Nona base URL. Env: NONA_CLI_BASE_URL. Default: http://localhost:18080.");
        var emailOpt = new Option<string?>("--email",
            "Admin email. Env: NONA_INIT_EMAIL.");
        var passwordOpt = new Option<string?>("--password",
            "Admin password. Env: NONA_INIT_PASSWORD. Use '-' to read one line from stdin.");
        var projectOpt = new Option<string?>(["--project", "--project-name"],
            "Project name. Env: NONA_CLI_PROJECT_NAME. Letters, numbers, and hyphens only.");
        var envOpt = new Option<string?>("--env",
            "Environment to create or reuse. Default: production.");
        var seedFlagOpt = new Option<string?>("--seed-flag",
            "Starter flag as key=value. Default: Features:Example=true.");
        var noSeedFlagOpt = new Option<bool>("--no-seed-flag",
            "Skip starter flag creation.");
        var scopeOpt = new Option<string?>("--scope",
            "API key and entry scope: client, server, or all. Default: client.");
        var formatOpt = new Option<string?>("--format",
            "Output format: dotenv, json, or env-export. Default: dotenv.");
        var printKeyOpt = new Option<bool>("--print-key",
            "Print the full API key. By default only the last four characters are shown.");
        var yesOpt = new Option<bool>("--yes",
            "Non-interactive mode; never prompt.");

        scopeOpt.AddValidator(result =>
        {
            var value = result.GetValueOrDefault<string>();
            if (!string.IsNullOrWhiteSpace(value) &&
                value.Trim().ToLowerInvariant() is not ("client" or "server" or "all"))
            {
                result.ErrorMessage = "Scope must be client, server, or all.";
            }
        });

        formatOpt.AddValidator(result =>
        {
            var value = result.GetValueOrDefault<string>();
            if (!string.IsNullOrWhiteSpace(value) &&
                value.Trim().ToLowerInvariant() is not ("dotenv" or "json" or "env-export"))
            {
                result.ErrorMessage = "Format must be dotenv, json, or env-export.";
            }
        });

        var cmd = new Command(
            "init",
            "Bootstrap a Nona instance from first container start to first flag read. Exit codes: 0 success; 1 unexpected/API error; 2 invalid args; 3 auth failed; 4 cannot reach base-url.");

        cmd.AddOption(baseUrlOpt);
        cmd.AddOption(emailOpt);
        cmd.AddOption(passwordOpt);
        cmd.AddOption(projectOpt);
        cmd.AddOption(envOpt);
        cmd.AddOption(seedFlagOpt);
        cmd.AddOption(noSeedFlagOpt);
        cmd.AddOption(scopeOpt);
        cmd.AddOption(formatOpt);
        cmd.AddOption(printKeyOpt);
        cmd.AddOption(yesOpt);

        cmd.Handler = CommandHandler.Create(async (InvocationContext ic) =>
        {
            var resolved = ResolveOptions(
                parsedBaseUrl: ic.ParseResult.GetValueForOption(baseUrlOpt),
                parsedEmail: ic.ParseResult.GetValueForOption(emailOpt),
                parsedPassword: ic.ParseResult.GetValueForOption(passwordOpt),
                parsedProject: ic.ParseResult.GetValueForOption(projectOpt),
                parsedEnvironment: ic.ParseResult.GetValueForOption(envOpt),
                parsedSeedFlag: ic.ParseResult.GetValueForOption(seedFlagOpt),
                noSeedFlag: ic.ParseResult.GetValueForOption(noSeedFlagOpt),
                parsedScope: ic.ParseResult.GetValueForOption(scopeOpt),
                parsedFormat: ic.ParseResult.GetValueForOption(formatOpt),
                printKey: ic.ParseResult.GetValueForOption(printKeyOpt),
                yes: ic.ParseResult.GetValueForOption(yesOpt));

            if (!resolved.Success)
            {
                Console.Error.WriteLine(resolved.Error);
                ic.ExitCode = 2;
                return;
            }

            ic.ExitCode = await new InitCommandHandler()
                .HandleAsync(resolved.Command!, ic.GetCancellationToken());
        });

        return cmd;
    }

    private InitCommandResolution ResolveOptions(
        string? parsedBaseUrl,
        string? parsedEmail,
        string? parsedPassword,
        string? parsedProject,
        string? parsedEnvironment,
        string? parsedSeedFlag,
        bool noSeedFlag,
        string? parsedScope,
        string? parsedFormat,
        bool printKey,
        bool yes)
    {
        var canPrompt = !yes && !Console.IsInputRedirected;

        var baseUrl = FirstValue(
            parsedBaseUrl,
            Environment.GetEnvironmentVariable("NONA_CLI_BASE_URL"),
            ctx.Defaults.BaseUrl,
            DefaultBaseUrl)!;

        baseUrl = baseUrl.Trim().TrimEnd('/');
        if (!IsHttpUrl(baseUrl))
            return InitCommandResolution.Fail("Init requires a valid HTTP(S) --base-url.");

        var email = FirstValue(parsedEmail, Environment.GetEnvironmentVariable("NONA_INIT_EMAIL"));
        if (string.IsNullOrWhiteSpace(email))
        {
            if (!canPrompt)
                return InitCommandResolution.Fail("Init requires --email or NONA_INIT_EMAIL.");

            email = PromptRequired("Admin email");
        }

        var password = FirstValue(parsedPassword, Environment.GetEnvironmentVariable("NONA_INIT_PASSWORD"));
        if (password == "-")
        {
            password = Console.In.ReadLine();
            if (string.IsNullOrWhiteSpace(password))
                return InitCommandResolution.Fail("Init could not read --password - from stdin.");
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            if (!canPrompt)
                return InitCommandResolution.Fail("Init requires --password, --password -, or NONA_INIT_PASSWORD.");

            password = PromptSecret("Admin password");
        }

        var project = FirstValue(
            parsedProject,
            Environment.GetEnvironmentVariable("NONA_CLI_PROJECT_NAME"),
            ctx.Defaults.Project);

        if (string.IsNullOrWhiteSpace(project))
        {
            if (!canPrompt)
                return InitCommandResolution.Fail("Init requires --project, NONA_CLI_PROJECT_NAME, or a saved default project.");

            project = PromptRequired("Project");
        }

        project = project.Trim();
        if (!IsSlug(project))
            return InitCommandResolution.Fail("Project must be alphanumeric with hyphens only.");

        var environment = FirstValue(parsedEnvironment, DefaultEnvironment)!.Trim();
        if (!IsSlug(environment))
            return InitCommandResolution.Fail("Environment must be alphanumeric with hyphens only.");

        var scope = FirstValue(parsedScope, "client")!.Trim().ToLowerInvariant();
        var format = FirstValue(parsedFormat, "dotenv")!.Trim().ToLowerInvariant();

        if (noSeedFlag && !string.IsNullOrWhiteSpace(parsedSeedFlag))
            return InitCommandResolution.Fail("Use either --seed-flag or --no-seed-flag, not both.");

        SeedFlag? seedFlag = null;
        if (!noSeedFlag)
        {
            var seedFlagValue = FirstValue(parsedSeedFlag, DefaultSeedFlag)!;
            if (!TryParseSeedFlag(seedFlagValue, out seedFlag, out var seedFlagError))
                return InitCommandResolution.Fail(seedFlagError!);
        }

        return InitCommandResolution.Ok(new InitCommand(
            BaseUrl: baseUrl,
            Email: email.Trim(),
            Password: password,
            Project: project,
            Environment: environment,
            SeedFlag: seedFlag,
            Scope: scope,
            Format: format,
            PrintKey: printKey));
    }

    private static string? FirstValue(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    private static bool TryParseSeedFlag(string value, out SeedFlag? seedFlag, out string? error)
    {
        seedFlag = null;
        error = null;

        var separator = value.IndexOf('=');
        if (separator <= 0)
        {
            error = "Seed flag must be formatted as key=value.";
            return false;
        }

        var key = value[..separator].Trim();
        var flagValue = value[(separator + 1)..];

        if (string.IsNullOrWhiteSpace(key) || key.Any(char.IsWhiteSpace))
        {
            error = "Seed flag key must be non-empty and contain no whitespace.";
            return false;
        }

        seedFlag = new SeedFlag(key, flagValue);
        return true;
    }

    private static string PromptRequired(string label)
    {
        while (true)
        {
            Console.Write($"{label}: ");
            var input = Console.ReadLine()?.Trim();
            if (!string.IsNullOrWhiteSpace(input))
                return input;

            Console.Error.WriteLine($"  {label} cannot be empty.");
        }
    }

    private static string PromptSecret(string label)
    {
        Console.Write($"{label}: ");
        var chars = new List<char>();

        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return new string([.. chars]);
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (chars.Count > 0)
                    chars.RemoveAt(chars.Count - 1);

                continue;
            }

            if (!char.IsControl(key.KeyChar))
                chars.Add(key.KeyChar);
        }
    }

    private static bool IsHttpUrl(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            uri.Scheme is "http" or "https";
    }

    private static bool IsSlug(string value)
        => value.Length > 0 && value.All(c => char.IsAsciiLetterOrDigit(c) || c == '-');
}

internal sealed record InitCommandResolution(bool Success, InitCommand? Command, string? Error)
{
    public static InitCommandResolution Ok(InitCommand command) => new(true, command, null);
    public static InitCommandResolution Fail(string error) => new(false, null, error);
}
