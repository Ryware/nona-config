using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using Nona.Cli.Environments.Commands;
using Nona.Cli.Environments.Queries;

namespace Nona.Cli.Environments;

internal sealed class EnvironmentsCommands(CliContext ctx) : ICliCommandGroup
{
    public Command Build()
    {
        var environments = new Command("environments", "List, create, and delete project environments.");

        var baseUrlOpt = new Option<string?>(["--base-url", "--api-url"], "Nona base URL.");
        var projectOpt = new Option<string?>(["--project", "--project-name"], "Nona project name.");
        var tokenOpt = new Option<string?>(["--token", "--bearer-token"], "Admin bearer token.");
        var nameOpt = new Option<string?>("--name", "Environment name. Letters, numbers, and hyphens only.")
        {
            IsRequired = true
        };

        environments.AddCommand(BuildList(baseUrlOpt, projectOpt, tokenOpt));
        environments.AddCommand(BuildCreate(baseUrlOpt, projectOpt, tokenOpt, nameOpt));
        environments.AddCommand(BuildDelete(baseUrlOpt, projectOpt, tokenOpt, nameOpt));
        return environments;
    }

    private Command BuildList(
        Option<string?> baseUrlOpt,
        Option<string?> projectOpt,
        Option<string?> tokenOpt)
    {
        var handler = new ListEnvironmentsQueryHandler();
        var cmd = new Command("list", "List environments for a project.");
        AddConnectionOptions(cmd, baseUrlOpt, projectOpt, tokenOpt);
        cmd.Handler = CommandHandler.Create(async (InvocationContext ic) =>
        {
            if (!TryResolveProject(ic, projectOpt, "list", out var project))
                return;

            var conn = ResolveConnection(ic, baseUrlOpt, tokenOpt);
            if (conn is null)
                return;

            ic.ExitCode = await handler.HandleAsync(
                new ListEnvironmentsQuery(conn, project!),
                ic.GetCancellationToken());
        });
        return cmd;
    }

    private Command BuildCreate(
        Option<string?> baseUrlOpt,
        Option<string?> projectOpt,
        Option<string?> tokenOpt,
        Option<string?> nameOpt)
    {
        var handler = new CreateEnvironmentCommandHandler();
        var cmd = new Command("create", "Create or reuse an environment.");
        AddConnectionOptions(cmd, baseUrlOpt, projectOpt, tokenOpt);
        cmd.AddOption(nameOpt);
        cmd.Handler = CommandHandler.Create(async (InvocationContext ic) =>
        {
            if (!TryResolveProject(ic, projectOpt, "create", out var project))
                return;

            var name = ic.ParseResult.GetValueForOption(nameOpt);
            if (string.IsNullOrWhiteSpace(name))
            {
                Console.Error.WriteLine("Environments create requires --name.");
                ic.ExitCode = 1;
                return;
            }

            var conn = ResolveConnection(ic, baseUrlOpt, tokenOpt);
            if (conn is null)
                return;

            ic.ExitCode = await handler.HandleAsync(
                new CreateEnvironmentCommand(conn, project!, name.Trim()),
                ic.GetCancellationToken());
        });
        return cmd;
    }

    private Command BuildDelete(
        Option<string?> baseUrlOpt,
        Option<string?> projectOpt,
        Option<string?> tokenOpt,
        Option<string?> nameOpt)
    {
        var handler = new DeleteEnvironmentCommandHandler();
        var cmd = new Command("delete", "Delete an environment.");
        AddConnectionOptions(cmd, baseUrlOpt, projectOpt, tokenOpt);
        cmd.AddOption(nameOpt);
        cmd.Handler = CommandHandler.Create(async (InvocationContext ic) =>
        {
            if (!TryResolveProject(ic, projectOpt, "delete", out var project))
                return;

            var name = ic.ParseResult.GetValueForOption(nameOpt);
            if (string.IsNullOrWhiteSpace(name))
            {
                Console.Error.WriteLine("Environments delete requires --name.");
                ic.ExitCode = 1;
                return;
            }

            var conn = ResolveConnection(ic, baseUrlOpt, tokenOpt);
            if (conn is null)
                return;

            ic.ExitCode = await handler.HandleAsync(
                new DeleteEnvironmentCommand(conn, project!, name.Trim()),
                ic.GetCancellationToken());
        });
        return cmd;
    }

    private static void AddConnectionOptions(
        Command command,
        Option<string?> baseUrlOpt,
        Option<string?> projectOpt,
        Option<string?> tokenOpt)
    {
        command.AddOption(baseUrlOpt);
        command.AddOption(projectOpt);
        command.AddOption(tokenOpt);
    }

    private bool TryResolveProject(
        InvocationContext ic,
        Option<string?> projectOpt,
        string action,
        out string? project)
    {
        project = ctx.Resolver.Project(ic.ParseResult.GetValueForOption(projectOpt));
        if (!string.IsNullOrWhiteSpace(project))
            return true;

        Console.Error.WriteLine(
            $"Environments {action} requires --project, NONA_CLI_PROJECT_NAME, or a saved default project.");
        ic.ExitCode = 1;
        return false;
    }

    private NonaCliConnectionOptions? ResolveConnection(
        InvocationContext ic,
        Option<string?> baseUrlOpt,
        Option<string?> tokenOpt)
    {
        var conn = ctx.Resolver.ResolveConnection(
            ic.ParseResult.GetValueForOption(baseUrlOpt),
            ic.ParseResult.GetValueForOption(tokenOpt));

        if (conn.Success)
            return conn.Connection;

        Console.Error.WriteLine(conn.Error);
        ic.ExitCode = 1;
        return null;
    }
}
