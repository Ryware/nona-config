using Nona.Cli.Generated.Models;

namespace Nona.Cli.Users.Commands;

internal sealed record CreateUserCommand(
    NonaCliConnectionOptions Connection,
    string Name,
    string Email,
    string? Role,
    string? Scope);

internal sealed class CreateUserCommandHandler(Func<HttpClient>? httpClientFactory = null)
{


    public async Task<int> HandleAsync(CreateUserCommand command, CancellationToken ct)
    {

        var api = NonaClientFactory.Create(command.Connection, httpClientFactory);
        var result = await api.Admin.Users.PostAsync(new CreateUserRequest
        {
            Name = command.Name,
            Email = command.Email,
            Role = command.Role,
            Scope = command.Scope
        }, cancellationToken: ct);

        Console.WriteLine($"Created user: {result!.User?.Name} <{result.User?.Email}>");
        Console.WriteLine($"Role:  {result.User?.Role}");
        Console.WriteLine($"Scope: {result.User?.Scope}");
        Console.WriteLine($"Invitation token: {result.InvitationToken}");
        return 0;
    }
}
