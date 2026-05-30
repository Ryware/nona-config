namespace Nona.Cli.Users.Commands;

internal sealed record CreateUserCommand(
    NonaCliConnectionOptions Connection,
    string Name,
    string Email,
    string? Role,
    string? Scope);

internal sealed class CreateUserCommandHandler
{
    private readonly Func<HttpClient> _createClient;

    internal CreateUserCommandHandler(Func<HttpClient>? httpClientFactory = null)
        { _createClient = httpClientFactory ?? (() => new HttpClient()); }

    public async Task<int> HandleAsync(CreateUserCommand command, CancellationToken ct)
    {
        using var http = _createClient();
        var api = new NonaApiClient(command.Connection.BaseUrl, command.Connection.BearerToken!, http);
        var result = await api.CreateUserAsync(command.Name, command.Email, command.Role, command.Scope, ct);

        Console.WriteLine($"Created user: {result.Name} <{result.Email}>");
        Console.WriteLine($"Role:  {result.Role}");
        Console.WriteLine($"Scope: {result.Scope}");
        Console.WriteLine($"Invitation token: {result.InvitationToken}");
        return 0;
    }
}
