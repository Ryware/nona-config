using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using Nona.Cli.Projects.Commands;
using Nona.Cli.Projects.Queries;

namespace Nona.Cli.Projects;

internal sealed class ProjectsCommands(CliContext ctx) : ICliCommandGroup
{
    public Command Build()
    {
        var projects = new Command("projects", "Manage Nona projects.");

        var baseUrlOpt = new Option<string?>(["--base-url", "--api-url"], "Nona API base URL.");
        var tokenOpt = new Option<string?>(["--token", "--bearer-token"], "Bearer token.");
        var projectOpt = new Option<string?>(["--project", "--project-name"], "Project name.");

        projects.AddCommand(BuildList(baseUrlOpt, tokenOpt));
        projects.AddCommand(BuildCreate(baseUrlOpt, tokenOpt));
        projects.AddCommand(BuildDelete(baseUrlOpt, tokenOpt, projectOpt));
        return projects;
    }

    private Command BuildList(Option<string?> baseUrlOpt, Option<string?> tokenOpt)
    {
        var handler = new ListProjectsQueryHandler();
        var cmd = new Command("list", "List all projects.");
        cmd.AddOption(baseUrlOpt);
        cmd.AddOption(tokenOpt);
        cmd.Handler = CommandHandler.Create(async (InvocationContext ic) =>
        {
            var conn = ctx.Resolver.ResolveConnection(
                ic.ParseResult.GetValueForOption(baseUrlOpt),
                ic.ParseResult.GetValueForOption(tokenOpt));

            if (!conn.Success) { Console.Error.WriteLine(conn.Error); ic.ExitCode = 1; return; }

            ic.ExitCode = await handler.HandleAsync(new ListProjectsQuery(conn.Connection!), ic.GetCancellationToken());
        });
        return cmd;
    }

    private Command BuildCreate(Option<string?> baseUrlOpt, Option<string?> tokenOpt)
    {
        var nameOpt = new Option<string?>("--name", "Project name (alphanumeric and hyphens).");
        var handler = new CreateProjectCommandHandler();
        var cmd = new Command("create", "Create a new project.");
        cmd.AddOption(baseUrlOpt);
        cmd.AddOption(tokenOpt);
        cmd.AddOption(nameOpt);
        cmd.Handler = CommandHandler.Create(async (InvocationContext ic) =>
        {
            var conn = ctx.Resolver.ResolveConnection(
                ic.ParseResult.GetValueForOption(baseUrlOpt),
                ic.ParseResult.GetValueForOption(tokenOpt));

            if (!conn.Success) { Console.Error.WriteLine(conn.Error); ic.ExitCode = 1; return; }

            var name = CliPrompter.Required(ic.ParseResult.GetValueForOption(nameOpt), "Project name");

            ic.ExitCode = await handler.HandleAsync(
                new CreateProjectCommand(conn.Connection!, name),
                ic.GetCancellationToken());
        });
        return cmd;
    }

    private Command BuildDelete(Option<string?> baseUrlOpt, Option<string?> tokenOpt, Option<string?> projectOpt)
    {
        var handler = new DeleteProjectCommandHandler();
        var cmd = new Command("delete", "Delete a project.");
        cmd.AddOption(baseUrlOpt);
        cmd.AddOption(tokenOpt);
        cmd.AddOption(projectOpt);
        cmd.Handler = CommandHandler.Create(async (InvocationContext ic) =>
        {
            var project = ctx.Resolver.Project(ic.ParseResult.GetValueForOption(projectOpt));
            if (string.IsNullOrWhiteSpace(project))
            {
                Console.Error.WriteLine("Projects delete requires --project, NONA_CLI_PROJECT_NAME, or a saved default project.");
                ic.ExitCode = 1;
                return;
            }

            var conn = ctx.Resolver.ResolveConnection(
                ic.ParseResult.GetValueForOption(baseUrlOpt),
                ic.ParseResult.GetValueForOption(tokenOpt));

            if (!conn.Success) { Console.Error.WriteLine(conn.Error); ic.ExitCode = 1; return; }

            ic.ExitCode = await handler.HandleAsync(
                new DeleteProjectCommand(conn.Connection!, project),
                ic.GetCancellationToken());
        });
        return cmd;
    }
}
