using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using Nona.Cli.Releases.Commands;
using Nona.Cli.Releases.Queries;

namespace Nona.Cli.Releases;

internal sealed class ReleasesCommands(CliContext ctx) : ICliCommandGroup
{
    public Command Build()
    {
        var releases = new Command(
            "releases",
            "List, inspect, publish, activate, and delete configuration releases.");

        var baseUrlOption = new Option<string?>(["--base-url", "--api-url"], "Nona base URL.");
        var tokenOption = new Option<string?>(["--token", "--bearer-token"], "Admin bearer token.");
        var projectOption = new Option<string?>(["--project", "--project-name"], "Nona project name.");
        var environmentOption = new Option<string?>(
            "--environment",
            "Environment containing the release.");
        var versionOption = new Option<string?>(
            "--version",
            "Release version in major.minor.patch format.");
        versionOption.AddValidator(result =>
        {
            var value = result.GetValueOrDefault<string>();
            if (!string.IsNullOrWhiteSpace(value) && !ReleaseVersions.TryParse(value, out _))
                result.ErrorMessage = "Version must use major.minor.patch format.";
        });

        releases.AddCommand(
            BuildList(baseUrlOption, tokenOption, projectOption, environmentOption));
        releases.AddCommand(
            BuildView(baseUrlOption, tokenOption, projectOption, environmentOption, versionOption));
        releases.AddCommand(
            BuildCreate(baseUrlOption, tokenOption, projectOption, environmentOption, versionOption));
        releases.AddCommand(BuildAmend());
        releases.AddCommand(
            BuildActivate(baseUrlOption, tokenOption, projectOption, environmentOption, versionOption));
        releases.AddCommand(
            BuildClearActive(baseUrlOption, tokenOption, projectOption, environmentOption));
        releases.AddCommand(
            BuildDelete(baseUrlOption, tokenOption, projectOption, environmentOption, versionOption));
        return releases;
    }

    private Command BuildList(
        Option<string?> baseUrlOption,
        Option<string?> tokenOption,
        Option<string?> projectOption,
        Option<string?> environmentOption)
    {
        var handler = new ListReleasesQueryHandler();
        var command = new Command("list", "List releases in an environment.");
        AddCommonOptions(
            command,
            baseUrlOption,
            tokenOption,
            projectOption,
            environmentOption);
        command.Handler = CommandHandler.Create(async (InvocationContext invocationContext) =>
        {
            var resolved = Resolve(
                invocationContext,
                baseUrlOption,
                tokenOption,
                projectOption,
                environmentOption);
            if (resolved is null)
                return;

            invocationContext.ExitCode = await handler.HandleAsync(
                new ListReleasesQuery(
                    resolved.Value.Connection,
                    resolved.Value.Project,
                    resolved.Value.Environment),
                invocationContext.GetCancellationToken());
        });
        return command;
    }

    private Command BuildView(
        Option<string?> baseUrlOption,
        Option<string?> tokenOption,
        Option<string?> projectOption,
        Option<string?> environmentOption,
        Option<string?> versionOption)
    {
        var handler = new ViewReleaseQueryHandler();
        var command = new Command("view", "Show one release and its entries.");
        AddCommonOptions(
            command,
            baseUrlOption,
            tokenOption,
            projectOption,
            environmentOption);
        command.AddOption(versionOption);
        command.Handler = CommandHandler.Create(async (InvocationContext invocationContext) =>
        {
            var resolved = Resolve(
                invocationContext,
                baseUrlOption,
                tokenOption,
                projectOption,
                environmentOption);
            if (resolved is null)
                return;

            var version = CliPrompter.Required(
                invocationContext.ParseResult.GetValueForOption(versionOption),
                "Version");
            invocationContext.ExitCode = await handler.HandleAsync(
                new ViewReleaseQuery(
                    resolved.Value.Connection,
                    resolved.Value.Project,
                    resolved.Value.Environment,
                    version),
                invocationContext.GetCancellationToken());
        });
        return command;
    }

    private Command BuildCreate(
        Option<string?> baseUrlOption,
        Option<string?> tokenOption,
        Option<string?> projectOption,
        Option<string?> environmentOption,
        Option<string?> versionOption)
    {
        var activateOption = new Option<bool>(
            "--activate",
            "Make the new release active immediately.");
        var handler = new CreateReleaseCommandHandler();
        var command = new Command(
            "create",
            "Publish the working configuration as a release.");
        AddCommonOptions(
            command,
            baseUrlOption,
            tokenOption,
            projectOption,
            environmentOption);
        command.AddOption(versionOption);
        command.AddOption(activateOption);
        command.Handler = CommandHandler.Create(async (InvocationContext invocationContext) =>
        {
            var resolved = Resolve(
                invocationContext,
                baseUrlOption,
                tokenOption,
                projectOption,
                environmentOption);
            if (resolved is null)
                return;

            var version = CliPrompter.Required(
                invocationContext.ParseResult.GetValueForOption(versionOption),
                "Version");
            invocationContext.ExitCode = await handler.HandleAsync(
                new CreateReleaseCommand(
                    resolved.Value.Connection,
                    resolved.Value.Project,
                    resolved.Value.Environment,
                    version,
                    invocationContext.ParseResult.GetValueForOption(activateOption)),
                invocationContext.GetCancellationToken());
        });
        return command;
    }

    private Command BuildAmend()
    {
        var baseUrlOption = new Option<string?>(["--base-url", "--api-url"], "Nona base URL.");
        var tokenOption = new Option<string?>(["--token", "--bearer-token"], "Admin bearer token.");
        var projectOption = new Option<string?>(["--project", "--project-name"], "Nona project name.");
        var environmentOption = new Option<string?>(
            "--environment",
            "Environment containing the release.");
        var sourceVersionOption = new Option<string?>(
            "--source-version",
            "Existing release version to copy.");
        var targetVersionOption = new Option<string?>(
            "--version",
            "New patch release version to publish.");

        var handler = new AmendReleaseCommandHandler();
        var command = new Command(
            "amend",
            "Publish a new release from an unchanged copy of an existing release.");
        command.AddOption(baseUrlOption);
        command.AddOption(tokenOption);
        command.AddOption(projectOption);
        command.AddOption(environmentOption);
        command.AddOption(sourceVersionOption);
        command.AddOption(targetVersionOption);
        command.Handler = CommandHandler.Create(async (InvocationContext invocationContext) =>
        {
            var project = ctx.Resolver.Project(
                invocationContext.ParseResult.GetValueForOption(projectOption));
            if (string.IsNullOrWhiteSpace(project))
            {
                Console.Error.WriteLine(
                    "Releases amend requires --project, NONA_CLI_PROJECT_NAME, " +
                    "or a saved default project.");
                invocationContext.ExitCode = CliExitCodes.UnexpectedError;
                return;
            }

            var connection = ctx.Resolver.ResolveConnection(
                invocationContext.ParseResult.GetValueForOption(baseUrlOption),
                invocationContext.ParseResult.GetValueForOption(tokenOption));
            if (!connection.Success)
            {
                Console.Error.WriteLine(connection.Error);
                invocationContext.ExitCode = CliExitCodes.UnexpectedError;
                return;
            }

            var environment = CliPrompter.Required(
                invocationContext.ParseResult.GetValueForOption(environmentOption),
                "Environment");
            var sourceVersion = CliPrompter.Required(
                invocationContext.ParseResult.GetValueForOption(sourceVersionOption),
                "Source version");
            var targetVersion = CliPrompter.Required(
                invocationContext.ParseResult.GetValueForOption(targetVersionOption),
                "New version");

            invocationContext.ExitCode = await handler.HandleAsync(
                new AmendReleaseCommand(
                    connection.Connection!,
                    project,
                    environment,
                    sourceVersion,
                    targetVersion),
                invocationContext.GetCancellationToken());
        });
        return command;
    }

    private Command BuildActivate(
        Option<string?> baseUrlOption,
        Option<string?> tokenOption,
        Option<string?> projectOption,
        Option<string?> environmentOption,
        Option<string?> versionOption)
    {
        var handler = new ActivateReleaseCommandHandler();
        var command = new Command("activate", "Make a release active.");
        AddCommonOptions(
            command,
            baseUrlOption,
            tokenOption,
            projectOption,
            environmentOption);
        command.AddOption(versionOption);
        command.Handler = CommandHandler.Create(async (InvocationContext invocationContext) =>
        {
            var resolved = Resolve(
                invocationContext,
                baseUrlOption,
                tokenOption,
                projectOption,
                environmentOption);
            if (resolved is null)
                return;

            var version = CliPrompter.Required(
                invocationContext.ParseResult.GetValueForOption(versionOption),
                "Version");
            invocationContext.ExitCode = await handler.HandleAsync(
                new ActivateReleaseCommand(
                    resolved.Value.Connection,
                    resolved.Value.Project,
                    resolved.Value.Environment,
                    version),
                invocationContext.GetCancellationToken());
        });
        return command;
    }

    private Command BuildClearActive(
        Option<string?> baseUrlOption,
        Option<string?> tokenOption,
        Option<string?> projectOption,
        Option<string?> environmentOption)
    {
        var handler = new ClearActiveReleaseCommandHandler();
        var command = new Command(
            "clear-active",
            "Clear the active release for an environment.");
        AddCommonOptions(
            command,
            baseUrlOption,
            tokenOption,
            projectOption,
            environmentOption);
        command.Handler = CommandHandler.Create(async (InvocationContext invocationContext) =>
        {
            var resolved = Resolve(
                invocationContext,
                baseUrlOption,
                tokenOption,
                projectOption,
                environmentOption);
            if (resolved is null)
                return;

            invocationContext.ExitCode = await handler.HandleAsync(
                new ClearActiveReleaseCommand(
                    resolved.Value.Connection,
                    resolved.Value.Project,
                    resolved.Value.Environment),
                invocationContext.GetCancellationToken());
        });
        return command;
    }

    private Command BuildDelete(
        Option<string?> baseUrlOption,
        Option<string?> tokenOption,
        Option<string?> projectOption,
        Option<string?> environmentOption,
        Option<string?> versionOption)
    {
        var handler = new DeleteReleaseCommandHandler();
        var command = new Command("delete", "Delete a non-active release.");
        AddCommonOptions(
            command,
            baseUrlOption,
            tokenOption,
            projectOption,
            environmentOption);
        command.AddOption(versionOption);
        command.Handler = CommandHandler.Create(async (InvocationContext invocationContext) =>
        {
            var resolved = Resolve(
                invocationContext,
                baseUrlOption,
                tokenOption,
                projectOption,
                environmentOption);
            if (resolved is null)
                return;

            var version = CliPrompter.Required(
                invocationContext.ParseResult.GetValueForOption(versionOption),
                "Version");
            invocationContext.ExitCode = await handler.HandleAsync(
                new DeleteReleaseCommand(
                    resolved.Value.Connection,
                    resolved.Value.Project,
                    resolved.Value.Environment,
                    version),
                invocationContext.GetCancellationToken());
        });
        return command;
    }

    private static void AddCommonOptions(
        Command command,
        Option<string?> baseUrlOption,
        Option<string?> tokenOption,
        Option<string?> projectOption,
        Option<string?> environmentOption)
    {
        command.AddOption(baseUrlOption);
        command.AddOption(tokenOption);
        command.AddOption(projectOption);
        command.AddOption(environmentOption);
    }

    private ResolvedReleaseContext? Resolve(
        InvocationContext invocationContext,
        Option<string?> baseUrlOption,
        Option<string?> tokenOption,
        Option<string?> projectOption,
        Option<string?> environmentOption)
    {
        var project = ctx.Resolver.Project(
            invocationContext.ParseResult.GetValueForOption(projectOption));
        if (string.IsNullOrWhiteSpace(project))
        {
            Console.Error.WriteLine(
                "Releases commands require --project, NONA_CLI_PROJECT_NAME, " +
                "or a saved default project.");
            invocationContext.ExitCode = CliExitCodes.UnexpectedError;
            return null;
        }

        var connection = ctx.Resolver.ResolveConnection(
            invocationContext.ParseResult.GetValueForOption(baseUrlOption),
            invocationContext.ParseResult.GetValueForOption(tokenOption));
        if (!connection.Success)
        {
            Console.Error.WriteLine(connection.Error);
            invocationContext.ExitCode = CliExitCodes.UnexpectedError;
            return null;
        }

        var environment = CliPrompter.Required(
            invocationContext.ParseResult.GetValueForOption(environmentOption),
            "Environment");
        return new ResolvedReleaseContext(connection.Connection!, project, environment);
    }

    private readonly record struct ResolvedReleaseContext(
        NonaCliConnectionOptions Connection,
        string Project,
        string Environment);
}
