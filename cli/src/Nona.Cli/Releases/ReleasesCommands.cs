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
            "Manage immutable configuration releases. Management commands use exact " +
            "major.minor.patch versions; major.minor.x is only a client-read selector.");

        releases.AddCommand(BuildList());
        releases.AddCommand(BuildView());
        releases.AddCommand(BuildCreate());
        releases.AddCommand(BuildAmend());
        releases.AddCommand(BuildActivate());
        releases.AddCommand(BuildClearActive());
        releases.AddCommand(BuildDelete());
        return releases;
    }

    private Command BuildList()
    {
        var common = CreateCommonOptions();
        var jsonOption = new Option<bool>(
            "--json",
            "Write one JSON array to standard output.");
        var handler = new ListReleasesQueryHandler();
        var command = new Command("list", "List releases in an environment.");
        AddCommonOptions(command, common);
        command.AddOption(jsonOption);
        command.Handler = CommandHandler.Create(async (InvocationContext invocationContext) =>
        {
            var resolved = Resolve(invocationContext, common);
            if (resolved is null)
                return;

            invocationContext.ExitCode = await handler.HandleAsync(
                new ListReleasesQuery(
                    resolved.Value.Connection,
                    resolved.Value.Project,
                    resolved.Value.Environment,
                    invocationContext.ParseResult.GetValueForOption(jsonOption)),
                invocationContext.GetCancellationToken());
        });
        return command;
    }

    private Command BuildView()
    {
        var common = CreateCommonOptions();
        var versionArgument = CreateExactVersionArgument(
            "version",
            "Exact release version in major.minor.patch format.");
        var jsonOption = new Option<bool>(
            "--json",
            "Write one JSON object to standard output.");
        var handler = new ViewReleaseQueryHandler();
        var command = new Command("view", "Show one exact release and its entries.");
        AddCommonOptions(command, common);
        command.AddArgument(versionArgument);
        command.AddOption(jsonOption);
        command.Handler = CommandHandler.Create(async (InvocationContext invocationContext) =>
        {
            var resolved = Resolve(invocationContext, common);
            if (resolved is null)
                return;

            var version = ParseExact(
                invocationContext.ParseResult.GetValueForArgument(versionArgument));
            invocationContext.ExitCode = await handler.HandleAsync(
                new ViewReleaseQuery(
                    resolved.Value.Connection,
                    resolved.Value.Project,
                    resolved.Value.Environment,
                    version.ToString(),
                    invocationContext.ParseResult.GetValueForOption(jsonOption)),
                invocationContext.GetCancellationToken());
        });
        return command;
    }

    private Command BuildCreate()
    {
        var common = CreateCommonOptions();
        var versionArgument = CreateLineVersionArgument();
        var activateOption = new Option<bool>(
            "--activate",
            "Make the new release active immediately.");
        var handler = new CreateReleaseCommandHandler();
        var command = new Command(
            "create",
            "Snapshot the working configuration as a new release. Supply major.minor; " +
            "the stored release starts at patch .0.");
        AddCommonOptions(command, common);
        command.AddArgument(versionArgument);
        command.AddOption(activateOption);
        command.Handler = CommandHandler.Create(async (InvocationContext invocationContext) =>
        {
            var resolved = Resolve(invocationContext, common);
            if (resolved is null)
                return;

            var line = ParseLine(
                invocationContext.ParseResult.GetValueForArgument(versionArgument));
            invocationContext.ExitCode = await handler.HandleAsync(
                new CreateReleaseCommand(
                    resolved.Value.Connection,
                    resolved.Value.Project,
                    resolved.Value.Environment,
                    line.FirstRelease.ToString(),
                    invocationContext.ParseResult.GetValueForOption(activateOption)),
                invocationContext.GetCancellationToken());
        });
        return command;
    }

    private Command BuildAmend()
    {
        var common = CreateCommonOptions();
        var sourceVersionArgument = CreateExactVersionArgument(
            "source-version",
            "Exact existing release version to amend.");
        var setOption = new Option<string[]>(
            "--set",
            "Set key=value in the copied entries. Repeat for multiple keys.")
        {
            AllowMultipleArgumentsPerToken = false
        };
        var deleteOption = new Option<string[]>(
            "--delete",
            "Delete a key from the copied entries. Repeat for multiple keys.")
        {
            AllowMultipleArgumentsPerToken = false
        };
        var fromFileOption = new Option<string?>(
            "--from-file",
            "Replace the copied entries with a UTF-8 JSON entries array.");
        var editorOption = new Option<bool>(
            "--editor",
            "Edit the copied entries as JSON using VISUAL or EDITOR.");

        var handler = new AmendReleaseCommandHandler();
        var command = new Command(
            "amend",
            "Edit a client-side copy of an exact release and publish it as the next patch.");
        AddCommonOptions(command, common);
        command.AddArgument(sourceVersionArgument);
        command.AddOption(setOption);
        command.AddOption(deleteOption);
        command.AddOption(fromFileOption);
        command.AddOption(editorOption);
        command.Handler = CommandHandler.Create(async (InvocationContext invocationContext) =>
        {
            var resolved = Resolve(invocationContext, common);
            if (resolved is null)
                return;

            var sourceVersion = ParseExact(
                invocationContext.ParseResult.GetValueForArgument(sourceVersionArgument));
            invocationContext.ExitCode = await handler.HandleAsync(
                new AmendReleaseCommand(
                    resolved.Value.Connection,
                    resolved.Value.Project,
                    resolved.Value.Environment,
                    sourceVersion.ToString(),
                    invocationContext.ParseResult.GetValueForOption(setOption) ?? [],
                    invocationContext.ParseResult.GetValueForOption(deleteOption) ?? [],
                    invocationContext.ParseResult.GetValueForOption(fromFileOption),
                    invocationContext.ParseResult.GetValueForOption(editorOption)),
                invocationContext.GetCancellationToken());
        });
        return command;
    }

    private Command BuildActivate()
    {
        var common = CreateCommonOptions();
        var versionArgument = CreateExactVersionArgument(
            "version",
            "Exact release version in major.minor.patch format.");
        var handler = new ActivateReleaseCommandHandler();
        var command = new Command("activate", "Make an exact release active.");
        AddCommonOptions(command, common);
        command.AddArgument(versionArgument);
        command.Handler = CommandHandler.Create(async (InvocationContext invocationContext) =>
        {
            var resolved = Resolve(invocationContext, common);
            if (resolved is null)
                return;

            var version = ParseExact(
                invocationContext.ParseResult.GetValueForArgument(versionArgument));
            invocationContext.ExitCode = await handler.HandleAsync(
                new ActivateReleaseCommand(
                    resolved.Value.Connection,
                    resolved.Value.Project,
                    resolved.Value.Environment,
                    version.ToString()),
                invocationContext.GetCancellationToken());
        });
        return command;
    }

    private Command BuildClearActive()
    {
        var common = CreateCommonOptions();
        var handler = new ClearActiveReleaseCommandHandler();
        var command = new Command(
            "clear-active",
            "Clear the active release for an environment.");
        AddCommonOptions(command, common);
        command.Handler = CommandHandler.Create(async (InvocationContext invocationContext) =>
        {
            var resolved = Resolve(invocationContext, common);
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

    private Command BuildDelete()
    {
        var common = CreateCommonOptions();
        var versionArgument = CreateExactVersionArgument(
            "version",
            "Exact release version in major.minor.patch format.");
        var handler = new DeleteReleaseCommandHandler();
        var command = new Command("delete", "Delete a non-active exact release.");
        AddCommonOptions(command, common);
        command.AddArgument(versionArgument);
        command.Handler = CommandHandler.Create(async (InvocationContext invocationContext) =>
        {
            var resolved = Resolve(invocationContext, common);
            if (resolved is null)
                return;

            var version = ParseExact(
                invocationContext.ParseResult.GetValueForArgument(versionArgument));
            invocationContext.ExitCode = await handler.HandleAsync(
                new DeleteReleaseCommand(
                    resolved.Value.Connection,
                    resolved.Value.Project,
                    resolved.Value.Environment,
                    version.ToString()),
                invocationContext.GetCancellationToken());
        });
        return command;
    }

    private static CommonOptions CreateCommonOptions()
        => new(
            new Option<string?>(["--base-url", "--api-url"], "Nona base URL."),
            new Option<string?>(["--token", "--bearer-token"], "Admin bearer token."),
            new Option<string?>(["--project", "--project-name"], "Nona project name."),
            new Option<string?>(
                "--environment",
                "Environment containing the release."));

    private static void AddCommonOptions(Command command, CommonOptions options)
    {
        command.AddOption(options.BaseUrl);
        command.AddOption(options.Token);
        command.AddOption(options.Project);
        command.AddOption(options.Environment);
    }

    private static Argument<string> CreateLineVersionArgument()
    {
        var argument = new Argument<string>(
            "version",
            "New release line in major.minor format; stored as major.minor.0.");
        argument.AddValidator(result =>
        {
            if (!ReleaseVersions.TryParseLine(result.GetValueOrDefault<string>(), out _))
                result.ErrorMessage = "Version must use major.minor format.";
        });
        return argument;
    }

    private static Argument<string> CreateExactVersionArgument(
        string name,
        string description)
    {
        var argument = new Argument<string>(name, description);
        argument.AddValidator(result =>
        {
            if (!ReleaseVersions.TryParseExact(result.GetValueOrDefault<string>(), out _))
                result.ErrorMessage = "Version must use major.minor.patch format.";
        });
        return argument;
    }

    private static ReleaseVersionLine ParseLine(string value)
        => ReleaseVersions.TryParseLine(value, out var version)
            ? version
            : throw new InvalidOperationException("Validated release line could not be parsed.");

    private static ReleaseVersion ParseExact(string value)
        => ReleaseVersions.TryParseExact(value, out var version)
            ? version
            : throw new InvalidOperationException("Validated exact release version could not be parsed.");

    private ResolvedReleaseContext? Resolve(
        InvocationContext invocationContext,
        CommonOptions options)
    {
        var project = ctx.Resolver.Project(
            invocationContext.ParseResult.GetValueForOption(options.Project));
        if (string.IsNullOrWhiteSpace(project))
        {
            Console.Error.WriteLine(
                "Releases commands require --project, NONA_CLI_PROJECT_NAME, " +
                "or a saved default project.");
            invocationContext.ExitCode = CliExitCodes.UnexpectedError;
            return null;
        }

        var connection = ctx.Resolver.ResolveConnection(
            invocationContext.ParseResult.GetValueForOption(options.BaseUrl),
            invocationContext.ParseResult.GetValueForOption(options.Token));
        if (!connection.Success)
        {
            Console.Error.WriteLine(connection.Error);
            invocationContext.ExitCode = CliExitCodes.UnexpectedError;
            return null;
        }

        var environment = CliPrompter.Required(
            invocationContext.ParseResult.GetValueForOption(options.Environment),
            "Environment");
        return new ResolvedReleaseContext(connection.Connection!, project, environment);
    }

    private sealed record CommonOptions(
        Option<string?> BaseUrl,
        Option<string?> Token,
        Option<string?> Project,
        Option<string?> Environment);

    private readonly record struct ResolvedReleaseContext(
        NonaCliConnectionOptions Connection,
        string Project,
        string Environment);
}
