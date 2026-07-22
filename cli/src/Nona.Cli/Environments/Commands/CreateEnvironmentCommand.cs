using Nona.Cli.Environments.Queries;
using Nona.Cli.Generated.Models;

namespace Nona.Cli.Environments.Commands;

internal sealed record CreateEnvironmentCommand(
    NonaCliConnectionOptions Connection,
    string Project,
    string Name);

internal sealed class CreateEnvironmentCommandHandler(Func<HttpClient>? httpClientFactory = null)
{
    public async Task<int> HandleAsync(CreateEnvironmentCommand command, CancellationToken ct)
    {
        using var api = NonaClientFactory.Create(command.Connection, httpClientFactory);
        EnvironmentDto? environment;

        try
        {
            environment = await api.Admin.Projects[command.Project].Environments.PostAsync(
                new CreateEnvironmentRequest { Name = command.Name },
                cancellationToken: ct);
        }
        catch (ApiProblemDetails ex) when (ex.ResponseStatusCode == 409)
        {
            var environments = await api.Admin.Projects[command.Project].Environments
                .GetAsync(cancellationToken: ct);
            environment = environments?.FirstOrDefault(existing =>
                string.Equals(existing.Name, command.Name, StringComparison.OrdinalIgnoreCase));

            if (environment is null)
                throw;

            Console.WriteLine($"Environment already exists: {environment.Name}");
            ListEnvironmentsQueryHandler.WriteEnvironment(environment);
            return 0;
        }

        Console.WriteLine($"Created environment: {environment!.Name}");
        ListEnvironmentsQueryHandler.WriteEnvironment(environment);
        return 0;
    }
}
