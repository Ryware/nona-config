using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using Nona.Migrator.Core.DTO;
using Nona.Migrator.Core.Options;
using Nona.Migrator.Core.Services;
using Nona.Migrator.FirebaseRemoteConfig;

namespace Nona.Cli;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var defaultsStore = new CliDefaultsStore();
        var defaults = defaultsStore.Load();
        var sessionStore = new CliSessionStore();
        var session = sessionStore.Load();

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        var root = new RootCommand("Nona CLI for key management and Firebase Remote Config migrations.");
        root.AddCommand(BuildAuthCommand(defaults, session, sessionStore, cts));
        root.AddCommand(BuildConfigCommand(defaults, defaultsStore));
        root.AddCommand(BuildKeysCommand(defaults, session, cts));
        root.AddCommand(BuildUsersCommand(defaults, session, cts));
        root.AddCommand(BuildMigrateCommand(defaults, session, cts));

        try
        {
            return await root.InvokeAsync(args);
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Cancelled.");
            return 2;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static Command BuildAuthCommand(
        CliDefaults defaults,
        CliAuthSession? session,
        CliSessionStore sessionStore,
        CancellationTokenSource cts)
    {
        var auth = new Command("auth", "Manage authentication sessions.");
        var resolver = new CliValueResolver(defaults, session);

        var baseUrlOpt = new Option<string?>(new[] { "--base-url", "--api-url" }, "Nona API base URL.");
        var emailOpt = new Option<string?>("--email", "Email address.");
        var passwordOpt = new Option<string?>("--password", "Password.");

        var loginCmd = new Command("login", "Authenticate and save a session.");
        loginCmd.AddOption(baseUrlOpt);
        loginCmd.AddOption(emailOpt);
        loginCmd.AddOption(passwordOpt);
        loginCmd.Handler = CommandHandler.Create(async (InvocationContext ctx) =>
        {
            var baseUrl = resolver.BaseUrl(ctx.ParseResult.GetValueForOption(baseUrlOpt));
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                Console.Error.WriteLine("Login requires --base-url, NONA_CLI_BASE_URL, or a saved default base-url.");
                ctx.ExitCode = 1;
                return;
            }
            ctx.ExitCode = await LoginAsync(
                baseUrl,
                resolver.Email(ctx.ParseResult.GetValueForOption(emailOpt)),
                resolver.Password(ctx.ParseResult.GetValueForOption(passwordOpt)),
                sessionStore,
                cts.Token);
        });

        var logoutCmd = new Command("logout", "Remove saved session.");
        logoutCmd.Handler = CommandHandler.Create((InvocationContext ctx) =>
            ctx.ExitCode = Logout(sessionStore));

        var whoamiCmd = new Command("whoami", "Show current session info.");
        whoamiCmd.Handler = CommandHandler.Create((InvocationContext ctx) =>
            ctx.ExitCode = ShowCurrentSession(session, sessionStore));

        auth.AddCommand(loginCmd);
        auth.AddCommand(logoutCmd);
        auth.AddCommand(whoamiCmd);
        return auth;
    }

    private static Command BuildConfigCommand(CliDefaults defaults, CliDefaultsStore defaultsStore)
    {
        var config = new Command("config", "Manage saved CLI defaults.");

        var showCmd = new Command("show", "Show saved defaults.");
        showCmd.Handler = CommandHandler.Create((InvocationContext ctx) =>
            ctx.ExitCode = ShowDefaults(defaults, defaultsStore));

        var nameArg = new Argument<string>("setting", "Setting to configure: base-url, project.");
        nameArg.AddValidator(result =>
        {
            var value = result.GetValueOrDefault<string>();
            if (value is not null && CliValueResolver.NormalizeConfigSettingName(value) is null)
                result.ErrorMessage = $"Unknown setting '{value}'. Valid settings: base-url, project.";
        });
        var valueArg = new Argument<string>("value", "The new value.");
        var setCmd = new Command("set", "Save a CLI default.");
        setCmd.AddArgument(nameArg);
        setCmd.AddArgument(valueArg);
        setCmd.Handler = CommandHandler.Create((InvocationContext ctx) =>
        {
            var name = ctx.ParseResult.GetValueForArgument(nameArg);
            var value = ctx.ParseResult.GetValueForArgument(valueArg);
            ctx.ExitCode = SetDefault(CliValueResolver.NormalizeConfigSettingName(name)!, value, defaults, defaultsStore);
        });

        config.AddCommand(showCmd);
        config.AddCommand(setCmd);
        return config;
    }

    private static Command BuildKeysCommand(
        CliDefaults defaults,
        CliAuthSession? session,
        CancellationTokenSource cts)
    {
        var keys = new Command("keys", "Manage project API keys.");
        var resolver = new CliValueResolver(defaults, session);

        var baseUrlOpt = new Option<string?>(new[] { "--base-url", "--api-url" }, "Nona API base URL.");
        var projectOpt = new Option<string?>(new[] { "--project", "--project-name" }, "Project name.");
        var tokenOpt = new Option<string?>(new[] { "--token", "--bearer-token" }, "Bearer token.");
        var emailOpt = new Option<string?>("--email", "Email address.");
        var passwordOpt = new Option<string?>("--password", "Password.");
        var typeOpt = new Option<string?>(new[] { "--type", "--key-type" }, "Key type: server, client, or both.");

        var showCmd = new Command("show", "Show current API keys for a project.");
        showCmd.AddOption(baseUrlOpt);
        showCmd.AddOption(projectOpt);
        showCmd.AddOption(tokenOpt);
        showCmd.AddOption(emailOpt);
        showCmd.AddOption(passwordOpt);
        showCmd.Handler = CommandHandler.Create(async (InvocationContext ctx) =>
        {
            var project = resolver.Project(ctx.ParseResult.GetValueForOption(projectOpt));
            if (string.IsNullOrWhiteSpace(project))
            {
                Console.Error.WriteLine("Keys show requires --project, NONA_CLI_PROJECT_NAME, or a saved default project.");
                ctx.ExitCode = 1;
                return;
            }

            var conn = resolver.ResolveConnection(
                ctx.ParseResult.GetValueForOption(baseUrlOpt),
                ctx.ParseResult.GetValueForOption(tokenOpt),
                ctx.ParseResult.GetValueForOption(emailOpt),
                ctx.ParseResult.GetValueForOption(passwordOpt));

            if (!conn.Success)
            {
                Console.Error.WriteLine(conn.Error);
                ctx.ExitCode = 1;
                return;
            }

            ctx.ExitCode = await ShowKeysAsync(conn.Connection!, project, cts.Token);
        });

        var rerollCmd = new Command("reroll", "Generate new API keys for a project.");
        rerollCmd.AddOption(baseUrlOpt);
        rerollCmd.AddOption(projectOpt);
        rerollCmd.AddOption(typeOpt);
        rerollCmd.AddOption(tokenOpt);
        rerollCmd.AddOption(emailOpt);
        rerollCmd.AddOption(passwordOpt);
        rerollCmd.Handler = CommandHandler.Create(async (InvocationContext ctx) =>
        {
            var project = resolver.Project(ctx.ParseResult.GetValueForOption(projectOpt));
            if (string.IsNullOrWhiteSpace(project))
            {
                Console.Error.WriteLine("Keys reroll requires --project, NONA_CLI_PROJECT_NAME, or a saved default project.");
                ctx.ExitCode = 1;
                return;
            }

            var keyType = ctx.ParseResult.GetValueForOption(typeOpt);
            if (string.IsNullOrWhiteSpace(keyType))
            {
                Console.Error.WriteLine("Keys reroll requires --type server|client|both.");
                ctx.ExitCode = 1;
                return;
            }

            if (!IsValidKeyType(keyType))
            {
                Console.Error.WriteLine("Key reroll type must be server, client, or both.");
                ctx.ExitCode = 1;
                return;
            }

            var conn = resolver.ResolveConnection(
                ctx.ParseResult.GetValueForOption(baseUrlOpt),
                ctx.ParseResult.GetValueForOption(tokenOpt),
                ctx.ParseResult.GetValueForOption(emailOpt),
                ctx.ParseResult.GetValueForOption(passwordOpt));

            if (!conn.Success)
            {
                Console.Error.WriteLine(conn.Error);
                ctx.ExitCode = 1;
                return;
            }

            ctx.ExitCode = await RerollKeysAsync(conn.Connection!, project, keyType.ToLowerInvariant(), cts.Token);
        });

        keys.AddCommand(showCmd);
        keys.AddCommand(rerollCmd);
        return keys;
    }

    private static Command BuildUsersCommand(
        CliDefaults defaults,
        CliAuthSession? session,
        CancellationTokenSource cts)
    {
        var users = new Command("users", "Manage Nona users.");
        var resolver = new CliValueResolver(defaults, session);

        var baseUrlOpt = new Option<string?>(new[] { "--base-url", "--api-url" }, "Nona API base URL.");
        var tokenOpt = new Option<string?>(new[] { "--token", "--bearer-token" }, "Bearer token.");
        var emailAuthOpt = new Option<string?>("--email", "Email address for authentication.");
        var passwordOpt = new Option<string?>("--password", "Password for authentication.");

        var nameOpt = new Option<string>("--name", "Full name of the new user.") { IsRequired = true };
        var userEmailOpt = new Option<string>("--user-email", "Email address of the new user.") { IsRequired = true };
        var roleOpt = new Option<string?>("--role", "Role: viewer or editor.");
        var scopeOpt = new Option<string?>("--scope", "Scope: client, server, or all.");

        roleOpt.AddValidator(result =>
        {
            var value = result.GetValueOrDefault<string>();
            if (value is not null && value is not "viewer" and not "editor")
                result.ErrorMessage = $"Unknown role '{value}'. Valid roles: viewer, editor.";
        });
        scopeOpt.AddValidator(result =>
        {
            var value = result.GetValueOrDefault<string>();
            if (value is not null && value is not "client" and not "server" and not "all")
                result.ErrorMessage = $"Unknown scope '{value}'. Valid scopes: client, server, all.";
        });

        var createCmd = new Command("create", "Create a new user and display their invitation token.");
        createCmd.AddOption(baseUrlOpt);
        createCmd.AddOption(tokenOpt);
        createCmd.AddOption(emailAuthOpt);
        createCmd.AddOption(passwordOpt);
        createCmd.AddOption(nameOpt);
        createCmd.AddOption(userEmailOpt);
        createCmd.AddOption(roleOpt);
        createCmd.AddOption(scopeOpt);
        createCmd.Handler = CommandHandler.Create(async (InvocationContext ctx) =>
        {
            var conn = resolver.ResolveConnection(
                ctx.ParseResult.GetValueForOption(baseUrlOpt),
                ctx.ParseResult.GetValueForOption(tokenOpt),
                ctx.ParseResult.GetValueForOption(emailAuthOpt),
                ctx.ParseResult.GetValueForOption(passwordOpt));

            if (!conn.Success)
            {
                Console.Error.WriteLine(conn.Error);
                ctx.ExitCode = 1;
                return;
            }

            ctx.ExitCode = await CreateUserAsync(
                conn.Connection!,
                ctx.ParseResult.GetValueForOption(nameOpt)!,
                ctx.ParseResult.GetValueForOption(userEmailOpt)!,
                ctx.ParseResult.GetValueForOption(roleOpt),
                ctx.ParseResult.GetValueForOption(scopeOpt),
                cts.Token);
        });

        users.AddCommand(createCmd);
        return users;
    }

    private static async Task<int> CreateUserAsync(
        NonaCliConnectionOptions connection,
        string name,
        string email,
        string? role,
        string? scope,
        CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient();
        var client = new NonaAdminClient(httpClient, connection.ToNonaOptions(string.Empty));
        var result = await client.CreateUserAsync(name, email, role, scope, cancellationToken);

        Console.WriteLine($"Created user: {result.User.Name} <{result.User.Email}>");
        Console.WriteLine($"Role:  {result.User.Role}");
        Console.WriteLine($"Scope: {result.User.Scope}");
        Console.WriteLine($"Invitation token: {result.InvitationToken}");
        return 0;
    }

    private static Command BuildMigrateCommand(
        CliDefaults defaults,
        CliAuthSession? session,
        CancellationTokenSource cts)
    {
        var migrate = new Command("migrate", "Run config migrations.");
        var resolver = new CliValueResolver(defaults, session);

        var configOpt = new Option<string?>("--config", "Path to the migration config file.");
        var dryRunOpt = new Option<bool>("--dry-run", "Preview changes without applying them.");
        var baseUrlOpt = new Option<string?>(new[] { "--base-url", "--api-url" }, "Nona API base URL.");
        var projectOpt = new Option<string?>(new[] { "--project", "--project-name" }, "Project name.");
        var tokenOpt = new Option<string?>(new[] { "--token", "--bearer-token" }, "Bearer token.");
        var emailOpt = new Option<string?>("--email", "Email address.");
        var passwordOpt = new Option<string?>("--password", "Password.");

        var firebaseCmd = new Command("firebase", "Migrate from Firebase Remote Config.");
        firebaseCmd.AddOption(configOpt);
        firebaseCmd.AddOption(dryRunOpt);
        firebaseCmd.AddOption(baseUrlOpt);
        firebaseCmd.AddOption(projectOpt);
        firebaseCmd.AddOption(tokenOpt);
        firebaseCmd.AddOption(emailOpt);
        firebaseCmd.AddOption(passwordOpt);
        firebaseCmd.Handler = CommandHandler.Create(async (InvocationContext ctx) =>
        {
            var baseUrl = resolver.BaseUrl(ctx.ParseResult.GetValueForOption(baseUrlOpt));
            var project = resolver.Project(ctx.ParseResult.GetValueForOption(projectOpt));
            var email = resolver.Email(ctx.ParseResult.GetValueForOption(emailOpt));
            var password = resolver.Password(ctx.ParseResult.GetValueForOption(passwordOpt));
            var token = resolver.Token(ctx.ParseResult.GetValueForOption(tokenOpt));

            if (string.IsNullOrWhiteSpace(token) && string.IsNullOrWhiteSpace(email) &&
                session is not null && !session.IsExpired &&
                baseUrl is not null && session.MatchesBaseUrl(baseUrl))
            {
                token = session.Token;
            }

            var forwardedArgs = resolver.BuildFirebaseArgs(
                ctx.ParseResult.GetValueForOption(configOpt),
                ctx.ParseResult.GetValueForOption(dryRunOpt),
                baseUrl, project, token, email, password);

            ctx.ExitCode = await FirebaseRemoteConfigMigrationCommand.RunAsync(forwardedArgs, cts.Token);
        });

        migrate.AddCommand(firebaseCmd);
        return migrate;
    }

    private static async Task<int> LoginAsync(
        string baseUrl,
        string? email,
        string? password,
        CliSessionStore sessionStore,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            Console.Write("Email: ");
            email = Console.ReadLine()?.Trim();
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            Console.Error.WriteLine("Email is required.");
            return 1;
        }

        if (string.IsNullOrWhiteSpace(password))
            password = ReadPassword("Password: ");

        if (string.IsNullOrWhiteSpace(password))
        {
            Console.Error.WriteLine("Password is required.");
            return 1;
        }

        using var httpClient = new HttpClient();
        var client = new NonaAdminClient(httpClient, new NonaOptions
        {
            BaseUrl = baseUrl,
            ProjectName = string.Empty,
            Email = email,
            Password = password
        });

        var response = await client.LoginAsync(cancellationToken);
        var session = new CliAuthSession
        {
            BaseUrl = baseUrl,
            Token = response.Token,
            Username = response.Username,
            Role = response.Role,
            ExpiresAt = response.ExpiresAt,
            SavedAtUtc = DateTime.UtcNow
        };

        sessionStore.Save(session);

        Console.WriteLine($"Logged in as {response.Username}");
        Console.WriteLine($"Role: {response.Role}");
        Console.WriteLine($"Base URL: {baseUrl}");
        Console.WriteLine($"Session file: {sessionStore.FilePath}");
        Console.WriteLine($"Expires at: {response.ExpiresAt:O}");
        return 0;
    }

    private static int Logout(CliSessionStore sessionStore)
    {
        sessionStore.Clear();
        Console.WriteLine("Logged out.");
        Console.WriteLine($"Session file: {sessionStore.FilePath}");
        return 0;
    }

    private static int ShowCurrentSession(CliAuthSession? session, CliSessionStore sessionStore)
    {
        if (session is null)
        {
            Console.WriteLine("Not logged in.");
            Console.WriteLine($"Session file: {sessionStore.FilePath}");
            return 0;
        }

        Console.WriteLine("Current session");
        Console.WriteLine($"Username: {session.Username}");
        Console.WriteLine($"Role: {session.Role}");
        Console.WriteLine($"Base URL: {session.BaseUrl}");
        Console.WriteLine($"Expires at: {session.ExpiresAt:O}");
        Console.WriteLine($"Status: {(session.IsExpired ? "expired" : "active")}");
        Console.WriteLine($"Session file: {sessionStore.FilePath}");
        return 0;
    }

    private static int ShowDefaults(CliDefaults defaults, CliDefaultsStore defaultsStore)
    {
        Console.WriteLine("CLI defaults");
        Console.WriteLine($"Config file: {defaultsStore.FilePath}");
        Console.WriteLine($"Base URL: {defaults.BaseUrl ?? "(not set)"}");
        Console.WriteLine($"Project: {defaults.Project ?? "(not set)"}");
        return 0;
    }

    private static int SetDefault(string name, string value, CliDefaults defaults, CliDefaultsStore defaultsStore)
    {
        var updatedDefaults = name switch
        {
            "base-url" => defaults with { BaseUrl = value },
            "project" => defaults with { Project = value },
            _ => throw new InvalidOperationException($"Unsupported default '{name}'.")
        };

        defaultsStore.Save(updatedDefaults);

        Console.WriteLine($"Saved default {name}: {value}");
        Console.WriteLine($"Config file: {defaultsStore.FilePath}");
        return 0;
    }

    private static async Task<int> ShowKeysAsync(
        NonaCliConnectionOptions connection,
        string project,
        CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient();
        var client = new NonaAdminClient(httpClient, connection.ToNonaOptions(project));
        var projectDto = await client.GetProjectAsync(project, cancellationToken);

        if (projectDto is null)
        {
            Console.Error.WriteLine($"Project '{project}' was not found.");
            return 1;
        }

        WriteProject(connection.BaseUrl, "Current keys", projectDto);
        return 0;
    }

    private static async Task<int> RerollKeysAsync(
        NonaCliConnectionOptions connection,
        string project,
        string keyType,
        CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient();
        var client = new NonaAdminClient(httpClient, connection.ToNonaOptions(project));
        var projectDto = await client.RerollApiKeysAsync(project, keyType, cancellationToken);

        WriteProject(connection.BaseUrl, $"Rerolled {keyType} key(s)", projectDto);
        return 0;
    }

    private static void WriteProject(string baseUrl, string title, NonaProjectDto project)
    {
        Console.WriteLine(title);
        Console.WriteLine($"Base URL: {baseUrl}");
        Console.WriteLine($"Project: {project.Name}");
        Console.WriteLine($"Slug: {project.UrlSlug ?? "(none)"}");
        Console.WriteLine($"Server key: {project.ServerApiKey ?? "(none)"}");
        Console.WriteLine($"Client key: {project.ClientApiKey ?? "(none)"}");

        var environments = project.Environments.Count == 0
            ? "(none)"
            : string.Join(", ", project.Environments.OrderBy(static e => e, StringComparer.OrdinalIgnoreCase));
        Console.WriteLine($"Environments: {environments}");
    }

    private static string ReadPassword(string prompt)
    {
        Console.Write(prompt);
        var buffer = new List<char>();

        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                break;
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (buffer.Count == 0)
                    continue;

                buffer.RemoveAt(buffer.Count - 1);
                continue;
            }

            if (!char.IsControl(key.KeyChar))
                buffer.Add(key.KeyChar);
        }

        return new string(buffer.ToArray());
    }

    private static bool IsValidKeyType(string keyType) =>
        string.Equals(keyType, "server", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(keyType, "client", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(keyType, "both", StringComparison.OrdinalIgnoreCase);
}

internal sealed record NonaCliConnectionOptions(
    string BaseUrl,
    string? Email,
    string? Password,
    string? BearerToken)
{
    public NonaOptions ToNonaOptions(string projectName) => new()
    {
        BaseUrl = BaseUrl,
        ProjectName = projectName,
        Email = Email,
        Password = Password,
        BearerToken = BearerToken
    };
}
