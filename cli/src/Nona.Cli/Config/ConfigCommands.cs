using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using Nona.Cli.Config.Commands;
using Nona.Cli.Config.Queries;

namespace Nona.Cli.Config;

internal sealed class ConfigCommands(CliContext ctx) : ICliCommandGroup
{
    public Command Build()
    {
        var config = new Command("config", "Manage saved CLI defaults.");
        config.AddCommand(BuildShow());
        config.AddCommand(BuildSet());
        return config;
    }

    private Command BuildShow()
    {
        var handler = new ShowDefaultsQueryHandler(ctx.DefaultsStore);
        var cmd = new Command("show", "Show saved defaults.");
        cmd.Handler = CommandHandler.Create(async (InvocationContext ic) =>
            ic.ExitCode = await handler.HandleAsync(new ShowDefaultsQuery(), ic.GetCancellationToken()));
        return cmd;
    }

    private Command BuildSet()
    {
        var nameArg = new Argument<string>("setting", "Setting to configure: base-url, project.");
        nameArg.AddValidator(result =>
        {
            var value = result.GetValueOrDefault<string>();
            if (value is not null && CliValueResolver.NormalizeConfigSettingName(value) is null)
                result.ErrorMessage = $"Unknown setting '{value}'. Valid settings: base-url, project.";
        });
        var valueArg = new Argument<string>("value", "The new value.");
        var handler = new SetDefaultCommandHandler(ctx.DefaultsStore);

        var cmd = new Command("set", "Save a CLI default.");
        cmd.AddArgument(nameArg);
        cmd.AddArgument(valueArg);
        cmd.Handler = CommandHandler.Create(async (InvocationContext ic) =>
        {
            var name  = CliValueResolver.NormalizeConfigSettingName(ic.ParseResult.GetValueForArgument(nameArg))!;
            var value = ic.ParseResult.GetValueForArgument(valueArg);
            ic.ExitCode = await handler.HandleAsync(new SetDefaultCommand(name, value), ic.GetCancellationToken());
        });

        return cmd;
    }
}
