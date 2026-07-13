using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using Nona.Cli.Users.Commands;

namespace Nona.Cli.Users;

internal sealed class UsersCommands(CliContext ctx) : ICliCommandGroup
{
    public Command Build()
    {
        var users = new Command("users", "Invite users to Nona.");
        users.AddCommand(BuildCreate());
        return users;
    }

    private Command BuildCreate()
    {
        var baseUrlOpt = new Option<string?>(["--base-url", "--api-url"], "Nona base URL.");
        var tokenOpt = new Option<string?>(["--token", "--bearer-token"], "Admin bearer token.");
        var nameOpt = new Option<string>("--name", "Full name of the new user.") { IsRequired = true };
        var userEmailOpt = new Option<string>("--user-email", "Email address of the new user.") { IsRequired = true };
        var roleOpt = new Option<string?>("--role", "User role: viewer or editor.");
        var scopeOpt = new Option<string?>("--scope", "User scope: client, server, or all.");

        roleOpt.AddValidator(result =>
        {
            var v = result.GetValueOrDefault<string>();
            if (v is not null && v is not "viewer" and not "editor")
                result.ErrorMessage = $"Unknown role '{v}'. Valid roles: viewer, editor.";
        });
        scopeOpt.AddValidator(result =>
        {
            var v = result.GetValueOrDefault<string>();
            if (v is not null && v is not "client" and not "server" and not "all")
                result.ErrorMessage = $"Unknown scope '{v}'. Valid scopes: client, server, all.";
        });

        var handler = new CreateUserCommandHandler();
        var cmd = new Command("create", "Invite a user and print the invitation token.");
        cmd.AddOption(baseUrlOpt);
        cmd.AddOption(tokenOpt);
        cmd.AddOption(nameOpt);
        cmd.AddOption(userEmailOpt);
        cmd.AddOption(roleOpt);
        cmd.AddOption(scopeOpt);
        cmd.Handler = CommandHandler.Create(async (InvocationContext ic) =>
        {
            var conn = ctx.Resolver.ResolveConnection(
                ic.ParseResult.GetValueForOption(baseUrlOpt),
                ic.ParseResult.GetValueForOption(tokenOpt));

            if (!conn.Success) { Console.Error.WriteLine(conn.Error); ic.ExitCode = 1; return; }

            ic.ExitCode = await handler.HandleAsync(new CreateUserCommand(
                conn.Connection!,
                ic.ParseResult.GetValueForOption(nameOpt)!,
                ic.ParseResult.GetValueForOption(userEmailOpt)!,
                ic.ParseResult.GetValueForOption(roleOpt),
                ic.ParseResult.GetValueForOption(scopeOpt)),
                ic.GetCancellationToken());
        });

        return cmd;
    }
}
