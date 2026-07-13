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
        var config = new Command("config", "Show or save default CLI values.");
        config.AddCommand(BuildShow());
        config.AddCommand(BuildSet());
        return config;
    }

    private Command BuildShow()
    {
        var handler = new ShowDefaultsQueryHandler(ctx.DefaultsStore);
        var cmd = new Command("show", "Show saved default values.");
        cmd.Handler = CommandHandler.Create(async (InvocationContext ic) =>
            ic.ExitCode = await handler.HandleAsync(new ShowDefaultsQuery(), ic.GetCancellationToken()));
        return cmd;
    }

    private Command BuildSet()
    {
        var nameArg = new Argument<string>("setting", "Setting name: base-url or project.");
        nameArg.AddValidator(result =>
        {
            var value = result.GetValueOrDefault<string>();
            if (value is not null && CliValueResolver.NormalizeConfigSettingName(value) is null)
                result.ErrorMessage = $"Unknown setting '{value}'. Valid settings: base-url, project.";
        });
        var valueArg = new Argument<string>("value", "Value to save as the default.");
        var handler = new SetDefaultCommandHandler(ctx.DefaultsStore);

        var cmd = new Command("set", "Save a default base URL or project.");
        cmd.AddArgument(nameArg);
        cmd.AddArgument(valueArg);
        cmd.Handler = CommandHandler.Create(async (InvocationContext ic) =>
        {
            var name = CliValueResolver.NormalizeConfigSettingName(ic.ParseResult.GetValueForArgument(nameArg))!;
            var value = ic.ParseResult.GetValueForArgument(valueArg);
            ic.ExitCode = await handler.HandleAsync(new SetDefaultCommand(name, value), ic.GetCancellationToken());
        });

        return cmd;
    }
}
