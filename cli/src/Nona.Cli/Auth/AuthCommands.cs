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
        var auth = new Command("auth", "Sign in and manage saved sessions.");
        auth.AddCommand(BuildRegister());
        auth.AddCommand(BuildLogin());
        auth.AddCommand(BuildLogout());
        auth.AddCommand(BuildWhoAmI());
        return auth;
    }

    private Command BuildRegister()
    {
        var baseUrlOpt = new Option<string?>(new[] { "--base-url", "--api-url" }, "Nona base URL.");
        var emailOpt = new Option<string?>("--email", "Email address for the first admin.");
        var passwordOpt = new Option<string?>("--password", "Password for the first admin.");
        var noSaveSessionOpt = new Option<bool>("--no-save-session", "Do not save the returned session token.");
        var handler = new RegisterFirstAdminCommandHandler(ctx.SessionStore);

        var cmd = new Command("register", "Create the first admin account and save a session.");
        cmd.AddOption(baseUrlOpt);
        cmd.AddOption(emailOpt);
        cmd.AddOption(passwordOpt);
        cmd.AddOption(noSaveSessionOpt);
        cmd.Handler = CommandHandler.Create(async (InvocationContext ic) =>
        {
            var baseUrl = ctx.Resolver.BaseUrl(ic.ParseResult.GetValueForOption(baseUrlOpt));
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                Console.Error.WriteLine("Registration requires --base-url, NONA_CLI_BASE_URL, or a saved default base-url.");
                ic.ExitCode = 1;
                return;
            }

            var email = ctx.Resolver.Email(ic.ParseResult.GetValueForOption(emailOpt));
            if (string.IsNullOrWhiteSpace(email))
            {
                Console.Error.WriteLine("Registration requires --email or NONA_CLI_EMAIL.");
                ic.ExitCode = 1;
                return;
            }

            var password = ctx.Resolver.Password(ic.ParseResult.GetValueForOption(passwordOpt));
            if (string.IsNullOrWhiteSpace(password))
            {
                Console.Error.WriteLine("Registration requires --password or NONA_CLI_PASSWORD.");
                ic.ExitCode = 1;
                return;
            }

            ic.ExitCode = await handler.HandleAsync(
                new RegisterFirstAdminCommand(baseUrl, email, password, SaveSession: !ic.ParseResult.GetValueForOption(noSaveSessionOpt)),
                ic.GetCancellationToken());
        });

        return cmd;
    }

    private Command BuildLogin()
    {
        var baseUrlOpt = new Option<string?>(new[] { "--base-url", "--api-url" }, "Nona base URL.");
        var handler = new LoginCommandHandler(ctx.SessionStore);

        var cmd = new Command("login", "Open a browser sign-in flow and save a session.");
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
        var cmd = new Command("logout", "Delete the saved session.");
        cmd.Handler = CommandHandler.Create(async (InvocationContext ic) =>
            ic.ExitCode = await handler.HandleAsync(new LogoutCommand(), ic.GetCancellationToken()));
        return cmd;
    }

    private Command BuildWhoAmI()
    {
        var handler = new WhoAmIQueryHandler();
        var cmd = new Command("whoami", "Show the saved session user.");
        cmd.Handler = CommandHandler.Create(async (InvocationContext ic) =>
            ic.ExitCode = await handler.HandleAsync(new WhoAmIQuery(ctx.Session), ic.GetCancellationToken()));
        return cmd;
    }
}
