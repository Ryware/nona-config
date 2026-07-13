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
        var tokenOpt = new Option<string?>(["--token", "--bearer-token"], "Bearer token.");
        var projectOpt = new Option<string?>(["--project", "--project-name"], "Project name.");
        var envOpt = new Option<string?>("--environment", "Environment name.");
        var keyOpt = new Option<string?>("--key", "Config entry key.");

        entries.AddCommand(BuildList(baseUrlOpt, tokenOpt, projectOpt, envOpt));
        entries.AddCommand(BuildGet(baseUrlOpt, tokenOpt, projectOpt, envOpt, keyOpt));
        entries.AddCommand(BuildHistory(baseUrlOpt, tokenOpt, projectOpt, envOpt, keyOpt));
        entries.AddCommand(BuildSet(baseUrlOpt, tokenOpt, projectOpt, envOpt, keyOpt));
        entries.AddCommand(BuildRollback(baseUrlOpt, tokenOpt, projectOpt, envOpt, keyOpt));
        entries.AddCommand(BuildDelete(baseUrlOpt, tokenOpt, projectOpt, envOpt, keyOpt));
        entries.AddCommand(BuildShare(baseUrlOpt, tokenOpt, projectOpt, envOpt, keyOpt));
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

    private Command BuildHistory(
        Option<string?> baseUrlOpt, Option<string?> tokenOpt,
        Option<string?> projectOpt, Option<string?> envOpt, Option<string?> keyOpt)
    {
        var handler = new HistoryEntriesQueryHandler();
        var cmd = new Command("history", "List version history for a config entry.");
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
            var key = CliPrompter.Required(ic.ParseResult.GetValueForOption(keyOpt), "Key");

            ic.ExitCode = await handler.HandleAsync(
                new HistoryEntriesQuery(conn, project!, environment, key),
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
            var key = CliPrompter.Required(ic.ParseResult.GetValueForOption(keyOpt), "Key");

            ic.ExitCode = await handler.HandleAsync(
                new GetEntryQuery(conn, project!, environment, key),
                ic.GetCancellationToken());
        });
        return cmd;
    }

    private Command BuildRollback(
        Option<string?> baseUrlOpt, Option<string?> tokenOpt,
        Option<string?> projectOpt, Option<string?> envOpt, Option<string?> keyOpt)
    {
        var versionOpt = new Option<int?>("--version", "Version number to roll back to.");
        versionOpt.AddValidator(result =>
        {
            var v = result.GetValueOrDefault<int?>();
            if (v is not null and <= 0)
                result.ErrorMessage = "Version must be greater than zero.";
        });

        var handler = new RollbackEntryCommandHandler();
        var cmd = new Command("rollback", "Roll a config entry back to a previous version.");
        cmd.AddOption(baseUrlOpt);
        cmd.AddOption(tokenOpt);
        cmd.AddOption(projectOpt);
        cmd.AddOption(envOpt);
        cmd.AddOption(keyOpt);
        cmd.AddOption(versionOpt);
        cmd.Handler = CommandHandler.Create(async (InvocationContext ic) =>
        {
            var (conn, project) = ResolveConnAndProject(ic, baseUrlOpt, tokenOpt, projectOpt);
            if (conn is null) return;

            var environment = CliPrompter.Required(ic.ParseResult.GetValueForOption(envOpt), "Environment");
            var key = CliPrompter.Required(ic.ParseResult.GetValueForOption(keyOpt), "Key");
            var version = CliPrompter.RequiredInt(ic.ParseResult.GetValueForOption(versionOpt), "Version");

            ic.ExitCode = await handler.HandleAsync(
                new RollbackEntryCommand(conn, project!, environment, key, version),
                ic.GetCancellationToken());
        });
        return cmd;
    }

    private Command BuildSet(
        Option<string?> baseUrlOpt, Option<string?> tokenOpt,
        Option<string?> projectOpt, Option<string?> envOpt, Option<string?> keyOpt)
    {
        var valueOpt = new Option<string?>("--value", "The config value.");
        var scopeOpt = new Option<string?>("--scope", "Scope: client, server, or all.");
        var contentTypeOpt = new Option<string?>("--content-type", "Logical content type: json, text, number, or boolean.");

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
            var key = CliPrompter.Required(ic.ParseResult.GetValueForOption(keyOpt), "Key");
            var value = CliPrompter.Required(ic.ParseResult.GetValueForOption(valueOpt), "Value");
            var scope = CliPrompter.Optional(ic.ParseResult.GetValueForOption(scopeOpt), "Scope (client/server/all)");
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
            var key = CliPrompter.Required(ic.ParseResult.GetValueForOption(keyOpt), "Key");

            ic.ExitCode = await handler.HandleAsync(
                new DeleteEntryCommand(conn, project!, environment, key),
                ic.GetCancellationToken());
        });
        return cmd;
    }

    private Command BuildShare(
        Option<string?> baseUrlOpt, Option<string?> tokenOpt,
        Option<string?> projectOpt, Option<string?> envOpt, Option<string?> keyOpt)
    {
        var share = new Command("share", "Manage temporary parameter share links.");

        share.AddCommand(BuildShareList(baseUrlOpt, tokenOpt, projectOpt, envOpt, keyOpt));
        share.AddCommand(BuildShareCreate(baseUrlOpt, tokenOpt, projectOpt, envOpt, keyOpt));
        share.AddCommand(BuildShareRevoke(baseUrlOpt, tokenOpt, projectOpt, envOpt, keyOpt));
        return share;
    }

    private Command BuildShareList(
        Option<string?> baseUrlOpt, Option<string?> tokenOpt,
        Option<string?> projectOpt, Option<string?> envOpt, Option<string?> keyOpt)
    {
        var handler = new ListEntryShareLinksQueryHandler();
        var cmd = new Command("list", "List share links for a config entry.");
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
            var key = CliPrompter.Required(ic.ParseResult.GetValueForOption(keyOpt), "Key");

            ic.ExitCode = await handler.HandleAsync(
                new ListEntryShareLinksQuery(conn, project!, environment, key),
                ic.GetCancellationToken());
        });
        return cmd;
    }

    private Command BuildShareCreate(
        Option<string?> baseUrlOpt, Option<string?> tokenOpt,
        Option<string?> projectOpt, Option<string?> envOpt, Option<string?> keyOpt)
    {
        var expirationOpt = new Option<string?>("--expiration", "Expiration: 1h, 1d, 3d, 30d, or 12m.");
        expirationOpt.AddValidator(result =>
        {
            var value = result.GetValueOrDefault<string>();
            if (!string.IsNullOrWhiteSpace(value) && !IsValidExpiration(value))
                result.ErrorMessage = "Expiration must be one of: 1h, 1d, 3d, 30d, 12m.";
        });
        var viewOnlyOpt = new Option<bool>("--view-only", "Create a view-only link instead of an editable link.");
        var shareBaseUrlOpt = new Option<string?>("--share-base-url", "Base URL for the printed browser link; defaults to the API base URL.");

        var handler = new CreateEntryShareLinkCommandHandler();
        var cmd = new Command("create", "Create a temporary share link for a config entry.");
        cmd.AddOption(baseUrlOpt);
        cmd.AddOption(tokenOpt);
        cmd.AddOption(projectOpt);
        cmd.AddOption(envOpt);
        cmd.AddOption(keyOpt);
        cmd.AddOption(expirationOpt);
        cmd.AddOption(viewOnlyOpt);
        cmd.AddOption(shareBaseUrlOpt);
        cmd.Handler = CommandHandler.Create(async (InvocationContext ic) =>
        {
            var (conn, project) = ResolveConnAndProject(ic, baseUrlOpt, tokenOpt, projectOpt);
            if (conn is null) return;

            var environment = CliPrompter.Required(ic.ParseResult.GetValueForOption(envOpt), "Environment");
            var key = CliPrompter.Required(ic.ParseResult.GetValueForOption(keyOpt), "Key");
            var expiration = ic.ParseResult.GetValueForOption(expirationOpt) ?? "1h";
            var canEdit = !ic.ParseResult.GetValueForOption(viewOnlyOpt);
            var shareBaseUrl = ic.ParseResult.GetValueForOption(shareBaseUrlOpt);

            ic.ExitCode = await handler.HandleAsync(
                new CreateEntryShareLinkCommand(conn, project!, environment, key, expiration, canEdit, shareBaseUrl),
                ic.GetCancellationToken());
        });
        return cmd;
    }

    private Command BuildShareRevoke(
        Option<string?> baseUrlOpt, Option<string?> tokenOpt,
        Option<string?> projectOpt, Option<string?> envOpt, Option<string?> keyOpt)
    {
        var idOpt = new Option<long?>("--id", "Share link id to revoke.");
        idOpt.AddValidator(result =>
        {
            var value = result.GetValueOrDefault<long?>();
            if (value is not null and <= 0)
                result.ErrorMessage = "Share link id must be greater than zero.";
        });

        var handler = new RevokeEntryShareLinkCommandHandler();
        var cmd = new Command("revoke", "Revoke a temporary share link.");
        cmd.AddOption(baseUrlOpt);
        cmd.AddOption(tokenOpt);
        cmd.AddOption(projectOpt);
        cmd.AddOption(envOpt);
        cmd.AddOption(keyOpt);
        cmd.AddOption(idOpt);
        cmd.Handler = CommandHandler.Create(async (InvocationContext ic) =>
        {
            var (conn, project) = ResolveConnAndProject(ic, baseUrlOpt, tokenOpt, projectOpt);
            if (conn is null) return;

            var environment = CliPrompter.Required(ic.ParseResult.GetValueForOption(envOpt), "Environment");
            var key = CliPrompter.Required(ic.ParseResult.GetValueForOption(keyOpt), "Key");
            var shareLinkId = RequiredPositiveLong(ic.ParseResult.GetValueForOption(idOpt), "Share link id");

            ic.ExitCode = await handler.HandleAsync(
                new RevokeEntryShareLinkCommand(conn, project!, environment, key, shareLinkId),
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

    private static bool IsValidExpiration(string value)
    {
        return value.Trim().ToLowerInvariant() is "1h" or "1d" or "3d" or "30d" or "12m";
    }

    private static long RequiredPositiveLong(long? provided, string label)
    {
        if (provided is > 0)
            return provided.Value;

        while (true)
        {
            var value = CliPrompter.Required(null, label);
            if (long.TryParse(value, out var parsed) && parsed > 0)
                return parsed;

            Console.Error.WriteLine($"  {label} must be a positive whole number.");
        }
    }
}
