using Nona.Migrator.Core.DTO;
using Nona.Migrator.Core.Options;
using Nona.Migrator.Core.Services;
using Nona.Migrator.FirebaseRemoteConfig;

namespace Nona.Cli;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellationTokenSource.Cancel();
        };

        var parseResult = CliParser.Parse(args);
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
