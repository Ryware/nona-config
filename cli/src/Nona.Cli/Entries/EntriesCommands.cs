using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using Nona.Cli.Entries.Commands;
using Nona.Cli.Entries.Queries;

namespace Nona.Cli.Entries;

internal sealed class EntriesCommands(CliContext ctx) : ICliCommandGroup
{
    public Command Build()
    {
        var entries = new Command("entries", "Manage config entries within a project environment.");

        var baseUrlOpt = new Option<string?>(["--base-url", "--api-url"], "Nona API base URL.");
        var tokenOpt   = new Option<string?>(["--token", "--bearer-token"], "Bearer token.");
        var projectOpt = new Option<string?>(["--project", "--project-name"], "Project name.");
        var envOpt     = new Option<string?>("--environment", "Environment name.");
        var keyOpt     = new Option<string?>("--key", "Config entry key.");

        entries.AddCommand(BuildList(baseUrlOpt, tokenOpt, projectOpt, envOpt));
        entries.AddCommand(BuildGet(baseUrlOpt, tokenOpt, projectOpt, envOpt, keyOpt));
        entries.AddCommand(BuildSet(baseUrlOpt, tokenOpt, projectOpt, envOpt, keyOpt));
        entries.AddCommand(BuildDelete(baseUrlOpt, tokenOpt, projectOpt, envOpt, keyOpt));
        return entries;
    }

    private Command BuildList(
        Option<string?> baseUrlOpt, Option<string?> tokenOpt,
        Option<string?> projectOpt, Option<string?> envOpt)
    {
        var handler = new ListEntriesQueryHandler();
        var cmd = new Command("list", "List all config entries in an environment.");
        cmd.AddOption(baseUrlOpt);
        cmd.AddOption(tokenOpt);
        cmd.AddOption(projectOpt);
        cmd.AddOption(envOpt);
        cmd.Handler = CommandHandler.Create(async (InvocationContext ic) =>
        {
            var (conn, project) = ResolveConnAndProject(ic, baseUrlOpt, tokenOpt, projectOpt);
            if (conn is null) return;

            var environment = CliPrompter.Required(ic.ParseResult.GetValueForOption(envOpt), "Environment");

            ic.ExitCode = await handler.HandleAsync(
                new ListEntriesQuery(conn, project!, environment),
                ic.GetCancellationToken());
        });
        return cmd;
    }

    private Command BuildGet(
        Option<string?> baseUrlOpt, Option<string?> tokenOpt,
        Option<string?> projectOpt, Option<string?> envOpt, Option<string?> keyOpt)
    {
        var handler = new GetEntryQueryHandler();
        var cmd = new Command("get", "Get a single config entry.");
        cmd.AddOption(baseUrlOpt);
        cmd.AddOption(tokenOpt);
        cmd.AddOption(projectOpt);
        cmd.AddOption(envOpt);
        cmd.AddOption(keyOpt);
        cmd.Handler = CommandHandler.Create(async (InvocationContext ic) =>
        {
            var (conn, project) = ResolveConnAndProject(ic, baseUrlOpt, tokenOpt, projectOpt);
            if (conn is null) return;

            var environment = CliPrompter.Required(ic.ParseResult.GetValueForOption(envOpt), "Environment");
            var key         = CliPrompter.Required(ic.ParseResult.GetValueForOption(keyOpt), "Key");

            ic.ExitCode = await handler.HandleAsync(
                new GetEntryQuery(conn, project!, environment, key),
                ic.GetCancellationToken());
        });
        return cmd;
    }

    private Command BuildSet(
        Option<string?> baseUrlOpt, Option<string?> tokenOpt,
        Option<string?> projectOpt, Option<string?> envOpt, Option<string?> keyOpt)
    {
        var valueOpt       = new Option<string?>("--value", "The config value.");
        var scopeOpt       = new Option<string?>("--scope", "Scope: client, server, or all.");
        var contentTypeOpt = new Option<string?>("--content-type", "MIME content type.");

        scopeOpt.AddValidator(result =>
        {
            var v = result.GetValueOrDefault<string>();
            if (v is not null && v is not "client" and not "server" and not "all")
                result.ErrorMessage = $"Unknown scope '{v}'. Valid scopes: client, server, all.";
        });

        var handler = new SetEntryCommandHandler();
        var cmd = new Command("set", "Create or update a config entry.");
        cmd.AddOption(baseUrlOpt);
        cmd.AddOption(tokenOpt);
        cmd.AddOption(projectOpt);
        cmd.AddOption(envOpt);
        cmd.AddOption(keyOpt);
        cmd.AddOption(valueOpt);
        cmd.AddOption(scopeOpt);
        cmd.AddOption(contentTypeOpt);
        cmd.Handler = CommandHandler.Create(async (InvocationContext ic) =>
        {
            var (conn, project) = ResolveConnAndProject(ic, baseUrlOpt, tokenOpt, projectOpt);
            if (conn is null) return;

            var environment = CliPrompter.Required(ic.ParseResult.GetValueForOption(envOpt), "Environment");
            var key         = CliPrompter.Required(ic.ParseResult.GetValueForOption(keyOpt), "Key");
            var value       = CliPrompter.Required(ic.ParseResult.GetValueForOption(valueOpt), "Value");
            var scope       = CliPrompter.Optional(ic.ParseResult.GetValueForOption(scopeOpt), "Scope (client/server/all)");
            var contentType = CliPrompter.Optional(ic.ParseResult.GetValueForOption(contentTypeOpt), "Content-Type");

            ic.ExitCode = await handler.HandleAsync(
                new SetEntryCommand(conn, project!, environment, key, value, scope, contentType),
                ic.GetCancellationToken());
        });
        return cmd;
    }

    private Command BuildDelete(
        Option<string?> baseUrlOpt, Option<string?> tokenOpt,
        Option<string?> projectOpt, Option<string?> envOpt, Option<string?> keyOpt)
    {
        var handler = new DeleteEntryCommandHandler();
        var cmd = new Command("delete", "Delete a config entry.");
        cmd.AddOption(baseUrlOpt);
        cmd.AddOption(tokenOpt);
        cmd.AddOption(projectOpt);
        cmd.AddOption(envOpt);
        cmd.AddOption(keyOpt);
        cmd.Handler = CommandHandler.Create(async (InvocationContext ic) =>
        {
            var (conn, project) = ResolveConnAndProject(ic, baseUrlOpt, tokenOpt, projectOpt);
            if (conn is null) return;

            var environment = CliPrompter.Required(ic.ParseResult.GetValueForOption(envOpt), "Environment");
            var key         = CliPrompter.Required(ic.ParseResult.GetValueForOption(keyOpt), "Key");

            ic.ExitCode = await handler.HandleAsync(
                new DeleteEntryCommand(conn, project!, environment, key),
                ic.GetCancellationToken());
        });
        return cmd;
    }

    private (NonaCliConnectionOptions? conn, string? project) ResolveConnAndProject(
        InvocationContext ic,
        Option<string?> baseUrlOpt,
        Option<string?> tokenOpt,
        Option<string?> projectOpt)
    {
        var project = CliPrompter.Required(
            ctx.Resolver.Project(ic.ParseResult.GetValueForOption(projectOpt)),
            "Project");

        var conn = ctx.Resolver.ResolveConnection(
            ic.ParseResult.GetValueForOption(baseUrlOpt),
            ic.ParseResult.GetValueForOption(tokenOpt));

        if (!conn.Success)
        {
            Console.Error.WriteLine(conn.Error);
            ic.ExitCode = 1;
            return (null, null);
        }

        return (conn.Connection!, project);
    }
}
