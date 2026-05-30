namespace Nona.Cli.Auth.Commands;

internal sealed record LoginCommand(string BaseUrl);

internal sealed class LoginCommandHandler(CliSessionStore sessionStore)
{
    public Task<int> HandleAsync(LoginCommand command, CancellationToken ct) =>
        BrowserLogin.RunAsync(command.BaseUrl, sessionStore, ct);
}
