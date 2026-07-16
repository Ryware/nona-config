using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using Nona.Cli.Migrate.Commands;

namespace Nona.Cli.Migrate;

internal sealed class MigrateCommands(CliContext ctx) : ICliCommandGroup
{
    public Command Build()
    {
        var migrate = new Command("migrate", "Run migration commands.");
        migrate.AddCommand(BuildFirebase());
        return migrate;
    }

    private Command BuildFirebase()
    {
        var configOpt = new Option<string?>("--config", "Migration config file path.");
        var dryRunOpt = new Option<bool>("--dry-run", "Preview changes without applying them.");
        var baseUrlOpt = new Option<string?>(["--base-url", "--api-url"], "Nona base URL.");
        var projectOpt = new Option<string?>(["--project", "--project-name"], "Nona project name.");
        var tokenOpt = new Option<string?>(["--token", "--bearer-token"], "Admin bearer token.");
        var emailOpt = new Option<string?>("--email", "Admin email used by the migrator when no token is supplied.");
        var passwordOpt = new Option<string?>("--password", "Admin password used by the migrator when no token is supplied.");

        var handler = new FirebaseMigrateCommandHandler();
        var cmd = new Command("firebase", "Import Firebase Remote Config into Nona.");
        cmd.AddOption(configOpt);
        cmd.AddOption(dryRunOpt);
        cmd.AddOption(baseUrlOpt);
        cmd.AddOption(projectOpt);
        cmd.AddOption(tokenOpt);
        cmd.AddOption(emailOpt);
        cmd.AddOption(passwordOpt);
        cmd.Handler = CommandHandler.Create(async (InvocationContext ic) =>
        {
            var baseUrl = ctx.Resolver.BaseUrl(ic.ParseResult.GetValueForOption(baseUrlOpt));
            var project = ctx.Resolver.Project(ic.ParseResult.GetValueForOption(projectOpt));
            var email = ctx.Resolver.Email(ic.ParseResult.GetValueForOption(emailOpt));
            var password = ctx.Resolver.Password(ic.ParseResult.GetValueForOption(passwordOpt));
            var token = ctx.Resolver.Token(ic.ParseResult.GetValueForOption(tokenOpt));

            if (string.IsNullOrWhiteSpace(token) && string.IsNullOrWhiteSpace(email) &&
                ctx.Session is not null && !ctx.Session.IsExpired &&
                baseUrl is not null && ctx.Session.MatchesBaseUrl(baseUrl))
            {
                token = ctx.Session.Token;
            }

            var args = ctx.Resolver.BuildFirebaseArgs(
                ic.ParseResult.GetValueForOption(configOpt),
                ic.ParseResult.GetValueForOption(dryRunOpt),
                baseUrl, project, token, email, password);

            ic.ExitCode = await handler.HandleAsync(new FirebaseMigrateCommand(args), ic.GetCancellationToken());
        });

        return cmd;
    }
}
