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
        migrate.AddCommand(BuildParameterStore());
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
            var connection = ctx.Resolver.ResolveMigrationConnection(
                ic.ParseResult.GetValueForOption(baseUrlOpt),
                ic.ParseResult.GetValueForOption(tokenOpt),
                ic.ParseResult.GetValueForOption(emailOpt),
                ic.ParseResult.GetValueForOption(passwordOpt));
            var project = ctx.Resolver.Project(ic.ParseResult.GetValueForOption(projectOpt));

            var args = ctx.Resolver.BuildFirebaseArgs(
                ic.ParseResult.GetValueForOption(configOpt),
                ic.ParseResult.GetValueForOption(dryRunOpt),
                connection.BaseUrl,
                project,
                connection.Token,
                connection.Email,
                connection.Password);

            ic.ExitCode = await handler.HandleAsync(new FirebaseMigrateCommand(args), ic.GetCancellationToken());
        });

        return cmd;
    }

    private Command BuildParameterStore()
    {
        var taskDefinitionOpt = new Option<string>(
            "--task-definition",
            "Local ECS task definition JSON path.")
        {
            IsRequired = true
        };
        var environmentOpt = new Option<string>(
            "--environment",
            "Target Nona environment.")
        {
            IsRequired = true
        };
        var regionOpt = new Option<string?>("--region", "AWS region for non-ARN Parameter Store references.");
        var profileOpt = new Option<string?>("--profile", "AWS shared credentials profile.");
        var dryRunOpt = new Option<bool>("--dry-run", "Preview changes without applying them.");
        var baseUrlOpt = new Option<string?>(["--base-url", "--api-url"], "Nona base URL.");
        var projectOpt = new Option<string?>(["--project", "--project-name"], "Nona project name.");
        var tokenOpt = new Option<string?>(["--token", "--bearer-token"], "Admin bearer token.");
        var emailOpt = new Option<string?>("--email", "Admin email used by the migrator when no token is supplied.");
        var passwordOpt = new Option<string?>("--password", "Admin password used by the migrator when no token is supplied.");

        var handler = new ParameterStoreMigrateCommandHandler();
        var cmd = new Command("parameter-store", "Import AWS Parameter Store values referenced by an ECS task definition into Nona.");
        cmd.AddOption(taskDefinitionOpt);
        cmd.AddOption(environmentOpt);
        cmd.AddOption(regionOpt);
        cmd.AddOption(profileOpt);
        cmd.AddOption(dryRunOpt);
        cmd.AddOption(baseUrlOpt);
        cmd.AddOption(projectOpt);
        cmd.AddOption(tokenOpt);
        cmd.AddOption(emailOpt);
        cmd.AddOption(passwordOpt);
        cmd.Handler = CommandHandler.Create(async (InvocationContext ic) =>
        {
            var connection = ctx.Resolver.ResolveMigrationConnection(
                ic.ParseResult.GetValueForOption(baseUrlOpt),
                ic.ParseResult.GetValueForOption(tokenOpt),
                ic.ParseResult.GetValueForOption(emailOpt),
                ic.ParseResult.GetValueForOption(passwordOpt));
            var project = ctx.Resolver.Project(ic.ParseResult.GetValueForOption(projectOpt));

            var args = ctx.Resolver.BuildParameterStoreArgs(
                ic.ParseResult.GetValueForOption(taskDefinitionOpt)!,
                ic.ParseResult.GetValueForOption(environmentOpt)!,
                ic.ParseResult.GetValueForOption(regionOpt),
                ic.ParseResult.GetValueForOption(profileOpt),
                ic.ParseResult.GetValueForOption(dryRunOpt),
                connection.BaseUrl,
                project,
                connection.Token,
                connection.Email,
                connection.Password);

            ic.ExitCode = await handler.HandleAsync(
                new ParameterStoreMigrateCommand(args),
                ic.GetCancellationToken());
        });

        return cmd;
    }
}
