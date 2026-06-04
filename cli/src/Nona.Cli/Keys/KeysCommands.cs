using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using Nona.Cli.Keys.Commands;
using Nona.Cli.Keys.Queries;

namespace Nona.Cli.Keys;

internal sealed class KeysCommands(CliContext ctx) : ICliCommandGroup
{
    private static readonly string[] ValidKeyTypes = ["server", "client", "both"];

    public Command Build()
    {
        var keys = new Command("keys", "Manage project API keys.");

        var baseUrlOpt = new Option<string?>(["--base-url", "--api-url"], "Nona API base URL.");
        var projectOpt = new Option<string?>(["--project", "--project-name"], "Project name.");
        var tokenOpt   = new Option<string?>(["--token", "--bearer-token"], "Bearer token.");
        var typeOpt    = new Option<string?>(["--type", "--key-type"], "Key type: server, client, or both.");

        keys.AddCommand(BuildShow(baseUrlOpt, projectOpt, tokenOpt));
        keys.AddCommand(BuildReroll(baseUrlOpt, projectOpt, typeOpt, tokenOpt));
        return keys;
    }

    private Command BuildShow(
        Option<string?> baseUrlOpt, Option<string?> projectOpt, Option<string?> tokenOpt)
    {
        var handler = new ShowKeysQueryHandler();
        var cmd = new Command("show", "Show current API keys for a project.");
        cmd.AddOption(baseUrlOpt);
        cmd.AddOption(projectOpt);
        cmd.AddOption(tokenOpt);
        cmd.Handler = CommandHandler.Create(async (InvocationContext ic) =>
        {
            var project = ctx.Resolver.Project(ic.ParseResult.GetValueForOption(projectOpt));
            if (string.IsNullOrWhiteSpace(project))
            {
                Console.Error.WriteLine("Keys show requires --project, NONA_CLI_PROJECT_NAME, or a saved default project.");
                ic.ExitCode = 1;
                return;
            }

            var conn = ctx.Resolver.ResolveConnection(
                ic.ParseResult.GetValueForOption(baseUrlOpt),
                ic.ParseResult.GetValueForOption(tokenOpt));

            if (!conn.Success) { Console.Error.WriteLine(conn.Error); ic.ExitCode = 1; return; }

            ic.ExitCode = await handler.HandleAsync(new ShowKeysQuery(conn.Connection!, project), ic.GetCancellationToken());
        });
        return cmd;
    }

    private Command BuildReroll(
        Option<string?> baseUrlOpt, Option<string?> projectOpt,
        Option<string?> typeOpt, Option<string?> tokenOpt)
    {
        var handler = new RerollKeysCommandHandler();
        var cmd = new Command("reroll", "Generate new API keys for a project.");
        cmd.AddOption(baseUrlOpt);
        cmd.AddOption(projectOpt);
        cmd.AddOption(typeOpt);
        cmd.AddOption(tokenOpt);
        cmd.Handler = CommandHandler.Create(async (InvocationContext ic) =>
        {
            var project = ctx.Resolver.Project(ic.ParseResult.GetValueForOption(projectOpt));
            if (string.IsNullOrWhiteSpace(project))
            {
                Console.Error.WriteLine("Keys reroll requires --project, NONA_CLI_PROJECT_NAME, or a saved default project.");
                ic.ExitCode = 1;
                return;
            }

            var keyType = ic.ParseResult.GetValueForOption(typeOpt);
            if (string.IsNullOrWhiteSpace(keyType))
            {
                Console.Error.WriteLine("Keys reroll requires --type server|client|both.");
                ic.ExitCode = 1;
                return;
            }

            if (!ValidKeyTypes.Contains(keyType, StringComparer.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine("Key reroll type must be server, client, or both.");
                ic.ExitCode = 1;
                return;
            }

            var conn = ctx.Resolver.ResolveConnection(
                ic.ParseResult.GetValueForOption(baseUrlOpt),
                ic.ParseResult.GetValueForOption(tokenOpt));

            if (!conn.Success) { Console.Error.WriteLine(conn.Error); ic.ExitCode = 1; return; }

            ic.ExitCode = await handler.HandleAsync(
                new RerollKeysCommand(conn.Connection!, project, keyType.ToLowerInvariant()),
                ic.GetCancellationToken());
        });
        return cmd;
    }
}
