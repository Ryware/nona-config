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

        using var cancellationTokenSource = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellationTokenSource.Cancel();
        };

        var parseResult = CliParser.Parse(args, defaults, session);
        if (!parseResult.Success)
        {
            Console.Error.WriteLine(parseResult.Error);
            Console.WriteLine(CliHelpText.Value);
            return 1;
        }

        if (parseResult.ShowHelp || parseResult.Command is null)
        {
            Console.WriteLine(CliHelpText.Value);
            return 0;
        }

        try
        {
            return parseResult.Command switch
            {
                LoginCommand command => await LoginAsync(command, sessionStore, cancellationTokenSource.Token),
                LogoutCommand => Logout(sessionStore),
                WhoAmICommand command => ShowCurrentSession(command, sessionStore),
                ShowCliDefaultsCommand command => ShowDefaults(command.Defaults, defaultsStore),
                SetCliDefaultCommand command => SetDefault(command, defaultsStore),
                ShowKeysCommand command => await ShowKeysAsync(command, cancellationTokenSource.Token),
                RerollKeysCommand command => await RerollKeysAsync(command, cancellationTokenSource.Token),
                MigrateFirebaseCommand command => await FirebaseRemoteConfigMigrationCommand.RunAsync(command.Arguments, cancellationTokenSource.Token),
                _ => 1
            };
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Cancelled.");
            return 2;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static async Task<int> LoginAsync(LoginCommand command, CliSessionStore sessionStore, CancellationToken cancellationToken)
    {
        var email = command.Email;
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

        var password = command.Password;
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
            BaseUrl = command.BaseUrl,
            ProjectName = string.Empty,
            Email = email,
            Password = password
        });

        var response = await client.LoginAsync(cancellationToken);
        var session = new CliAuthSession
        {
            BaseUrl = command.BaseUrl,
            Token = response.Token,
            Username = response.Username,
            Role = response.Role,
            ExpiresAt = response.ExpiresAt,
            SavedAtUtc = DateTime.UtcNow
        };

        sessionStore.Save(session);

        Console.WriteLine($"Logged in as {response.Username}");
        Console.WriteLine($"Role: {response.Role}");
        Console.WriteLine($"Base URL: {command.BaseUrl}");
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

    private static int ShowCurrentSession(WhoAmICommand command, CliSessionStore sessionStore)
    {
        if (command.Session is null)
        {
            Console.WriteLine("Not logged in.");
            Console.WriteLine($"Session file: {sessionStore.FilePath}");
            return 0;
        }

        Console.WriteLine("Current session");
        Console.WriteLine($"Username: {command.Session.Username}");
        Console.WriteLine($"Role: {command.Session.Role}");
        Console.WriteLine($"Base URL: {command.Session.BaseUrl}");
        Console.WriteLine($"Expires at: {command.Session.ExpiresAt:O}");
        Console.WriteLine($"Status: {(command.Session.IsExpired ? "expired" : "active")}");
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

    private static int SetDefault(SetCliDefaultCommand command, CliDefaultsStore defaultsStore)
    {
        var updatedDefaults = command.Name switch
        {
            "base-url" => command.Defaults with { BaseUrl = command.Value },
            "project" => command.Defaults with { Project = command.Value },
            _ => throw new InvalidOperationException($"Unsupported default '{command.Name}'.")
        };

        defaultsStore.Save(updatedDefaults);

        Console.WriteLine($"Saved default {command.Name}: {command.Value}");
        Console.WriteLine($"Config file: {defaultsStore.FilePath}");
        return 0;
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

    private static async Task<int> ShowKeysAsync(ShowKeysCommand command, CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient();
        var client = new NonaAdminClient(httpClient, command.Connection.ToNonaOptions(command.Project));
        var project = await client.GetProjectAsync(command.Project, cancellationToken);

        if (project is null)
        {
            Console.Error.WriteLine($"Project '{command.Project}' was not found.");
            return 1;
        }

        WriteProject(command.Connection.BaseUrl, "Current keys", project);
        return 0;
    }

    private static async Task<int> RerollKeysAsync(RerollKeysCommand command, CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient();
        var client = new NonaAdminClient(httpClient, command.Connection.ToNonaOptions(command.Project));
        var project = await client.RerollApiKeysAsync(command.Project, command.KeyType, cancellationToken);

        WriteProject(command.Connection.BaseUrl, $"Rerolled {command.KeyType} key(s)", project);
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
            : string.Join(", ", project.Environments.OrderBy(static environment => environment, StringComparer.OrdinalIgnoreCase));
        Console.WriteLine($"Environments: {environments}");
    }
}

internal abstract record CliCommand;

internal sealed record LoginCommand(string BaseUrl, string? Email, string? Password) : CliCommand;

internal sealed record LogoutCommand : CliCommand;

internal sealed record WhoAmICommand(CliAuthSession? Session) : CliCommand;

internal sealed record ShowCliDefaultsCommand(CliDefaults Defaults) : CliCommand;

internal sealed record SetCliDefaultCommand(string Name, string Value, CliDefaults Defaults) : CliCommand;

internal sealed record ShowKeysCommand(NonaCliConnectionOptions Connection, string Project) : CliCommand;

internal sealed record RerollKeysCommand(NonaCliConnectionOptions Connection, string Project, string KeyType) : CliCommand;

internal sealed record MigrateFirebaseCommand(string[] Arguments) : CliCommand;

internal sealed record NonaCliConnectionOptions(
    string BaseUrl,
    string? Email,
    string? Password,
    string? BearerToken)
{
    public NonaOptions ToNonaOptions(string projectName)
    {
        return new NonaOptions
        {
            BaseUrl = BaseUrl,
            ProjectName = projectName,
            Email = Email,
            Password = Password,
            BearerToken = BearerToken
        };
    }
}
