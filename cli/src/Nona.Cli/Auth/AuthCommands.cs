using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using Nona.Cli.Auth.Commands;
using Nona.Cli.Auth.Queries;

namespace Nona.Cli.Auth;

internal sealed class AuthCommands(CliContext ctx) : ICliCommandGroup
{
    public Command Build()
    {
        var auth = new Command("auth", "Manage authentication sessions.");
        auth.AddCommand(BuildLogin());
        auth.AddCommand(BuildLogout());
        auth.AddCommand(BuildWhoAmI());
        return auth;
    }

    private Command BuildLogin()
    {
        var baseUrlOpt = new Option<string?>(new[] { "--base-url", "--api-url" }, "Nona API base URL.");
        var handler = new LoginCommandHandler(ctx.SessionStore);

        var cmd = new Command("login", "Open a browser to log in and save a session.");
        cmd.AddOption(baseUrlOpt);
        cmd.Handler = CommandHandler.Create(async (InvocationContext ic) =>
        {
            var baseUrl = ctx.Resolver.BaseUrl(ic.ParseResult.GetValueForOption(baseUrlOpt));
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                Console.Error.WriteLine("Login requires --base-url, NONA_CLI_BASE_URL, or a saved default base-url.");
                ic.ExitCode = 1;
                return;
            }

            ic.ExitCode = await handler.HandleAsync(new LoginCommand(baseUrl), ic.GetCancellationToken());
        });

        return cmd;
    }

    private Command BuildLogout()
    {
        var handler = new LogoutCommandHandler(ctx.SessionStore);
        var cmd = new Command("logout", "Remove saved session.");
        cmd.Handler = CommandHandler.Create(async (InvocationContext ic) =>
            ic.ExitCode = await handler.HandleAsync(new LogoutCommand(), ic.GetCancellationToken()));
        return cmd;
    }

    private Command BuildWhoAmI()
    {
        var handler = new WhoAmIQueryHandler();
        var cmd = new Command("whoami", "Show current session info.");
        cmd.Handler = CommandHandler.Create(async (InvocationContext ic) =>
            ic.ExitCode = await handler.HandleAsync(new WhoAmIQuery(ctx.Session), ic.GetCancellationToken()));
        return cmd;
    }
}
