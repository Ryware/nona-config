namespace Nona.Cli.Auth.Commands;

internal sealed record LogoutCommand;

internal sealed class LogoutCommandHandler(CliSessionStore sessionStore)
{
    public Task<int> HandleAsync(LogoutCommand command, CancellationToken ct)
    {
        sessionStore.Clear();
        Console.WriteLine("Logged out.");
        return Task.FromResult(0);
    }
}
