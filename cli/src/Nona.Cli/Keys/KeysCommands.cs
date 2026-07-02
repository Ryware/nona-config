using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using Nona.Cli.Keys.Commands;
using Nona.Cli.Keys.Queries;

namespace Nona.Cli.Keys;

internal sealed class KeysCommands(CliContext ctx) : ICliCommandGroup
{
    private static readonly string[] ValidScopes = ["client", "server", "all"];

    public Command Build()
    {
        var keys = new Command("keys", "Manage project API keys.");

        var baseUrlOpt = new Option<string?>(["--base-url", "--api-url"], "Nona API base URL.");
        var projectOpt = new Option<string?>(["--project", "--project-name"], "Project name.");
        var tokenOpt = new Option<string?>(["--token", "--bearer-token"], "Bearer token.");
        var nameOpt = new Option<string?>(["--name"], "API key name.");
        var envOpt = new Option<string?>(["--environment", "--env"], "Optional environment scope.");
        var scopeOpt = new Option<string?>(["--scope"], "Config scope: client, server, or all.");
        var idOpt = new Option<string?>(["--id"], "API key id.");

        keys.AddCommand(BuildShow(baseUrlOpt, projectOpt, tokenOpt));
        keys.AddCommand(BuildCreate(baseUrlOpt, projectOpt, nameOpt, envOpt, scopeOpt, tokenOpt));
        keys.AddCommand(BuildDelete(baseUrlOpt, projectOpt, idOpt, tokenOpt));
        return keys;
    }

    private Command BuildShow(
        Option<string?> baseUrlOpt, Option<string?> projectOpt, Option<string?> tokenOpt)
    {
        var handler = new ShowKeysQueryHandler();
        var cmd = new Command("list", "List managed API keys for a project.");
        cmd.AddAlias("show");
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

    private Command BuildCreate(
        Option<string?> baseUrlOpt, Option<string?> projectOpt,
        Option<string?> nameOpt, Option<string?> envOpt, Option<string?> scopeOpt,
        Option<string?> tokenOpt)
    {
        var handler = new CreateApiKeyCommandHandler();
        var cmd = new Command("create", "Generate a managed API key for a project.");
        cmd.AddOption(baseUrlOpt);
        cmd.AddOption(projectOpt);
        cmd.AddOption(nameOpt);
        cmd.AddOption(envOpt);
        cmd.AddOption(scopeOpt);
        cmd.AddOption(tokenOpt);
        cmd.Handler = CommandHandler.Create(async (InvocationContext ic) =>
        {
            var project = ctx.Resolver.Project(ic.ParseResult.GetValueForOption(projectOpt));
            if (string.IsNullOrWhiteSpace(project))
            {
                Console.Error.WriteLine("Keys create requires --project, NONA_CLI_PROJECT_NAME, or a saved default project.");
                ic.ExitCode = 1;
                return;
            }

            var name = ic.ParseResult.GetValueForOption(nameOpt);
            if (string.IsNullOrWhiteSpace(name))
            {
                Console.Error.WriteLine("Keys create requires --name.");
                ic.ExitCode = 1;
                return;
            }

            var scope = ic.ParseResult.GetValueForOption(scopeOpt) ?? "client";
            if (!ValidScopes.Contains(scope, StringComparer.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine("Key scope must be client, server, or all.");
                ic.ExitCode = 1;
                return;
            }

            var conn = ctx.Resolver.ResolveConnection(
                ic.ParseResult.GetValueForOption(baseUrlOpt),
                ic.ParseResult.GetValueForOption(tokenOpt));

            if (!conn.Success) { Console.Error.WriteLine(conn.Error); ic.ExitCode = 1; return; }

            ic.ExitCode = await handler.HandleAsync(
                new CreateApiKeyCommand(
                    conn.Connection!,
                    project,
                    name.Trim(),
                    ic.ParseResult.GetValueForOption(envOpt),
                    scope.ToLowerInvariant()),
                ic.GetCancellationToken());
        });
        return cmd;
    }

    private Command BuildDelete(
        Option<string?> baseUrlOpt, Option<string?> projectOpt,
        Option<string?> idOpt, Option<string?> tokenOpt)
    {
        var handler = new DeleteApiKeyCommandHandler();
        var cmd = new Command("delete", "Delete a managed API key.");
        cmd.AddOption(baseUrlOpt);
        cmd.AddOption(projectOpt);
        cmd.AddOption(idOpt);
        cmd.AddOption(tokenOpt);
        cmd.Handler = CommandHandler.Create(async (InvocationContext ic) =>
        {
            var project = ctx.Resolver.Project(ic.ParseResult.GetValueForOption(projectOpt));
            if (string.IsNullOrWhiteSpace(project))
            {
                Console.Error.WriteLine("Keys delete requires --project, NONA_CLI_PROJECT_NAME, or a saved default project.");
                ic.ExitCode = 1;
                return;
            }

            var idValue = ic.ParseResult.GetValueForOption(idOpt);
            if (!long.TryParse(idValue, out var apiKeyId) || apiKeyId <= 0)
            {
                Console.Error.WriteLine("Keys delete requires --id with a positive API key id.");
                ic.ExitCode = 1;
                return;
            }

            var conn = ctx.Resolver.ResolveConnection(
                ic.ParseResult.GetValueForOption(baseUrlOpt),
                ic.ParseResult.GetValueForOption(tokenOpt));

            if (!conn.Success) { Console.Error.WriteLine(conn.Error); ic.ExitCode = 1; return; }

            ic.ExitCode = await handler.HandleAsync(
                new DeleteApiKeyCommand(conn.Connection!, project, apiKeyId),
                ic.GetCancellationToken());
        });
        return cmd;
    }

}
