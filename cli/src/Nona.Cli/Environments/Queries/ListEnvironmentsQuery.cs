using Nona.Cli.Generated.Models;

namespace Nona.Cli.Environments.Queries;

internal sealed record ListEnvironmentsQuery(
    NonaCliConnectionOptions Connection,
    string Project);

internal sealed class ListEnvironmentsQueryHandler(Func<HttpClient>? httpClientFactory = null)
{
    public async Task<int> HandleAsync(ListEnvironmentsQuery query, CancellationToken ct)
    {
        var api = NonaClientFactory.Create(query.Connection, httpClientFactory);
        var environments = await api.Admin.Projects[query.Project].Environments
            .GetAsync(cancellationToken: ct);

        if (environments is null || environments.Count == 0)
        {
            Console.WriteLine($"No environments found for project: {query.Project}");
            return 0;
        }

        Console.WriteLine($"Environments — {query.Project}");
        foreach (var environment in environments.OrderBy(environment => environment.Name))
            WriteEnvironment(environment);

        return 0;
    }

    internal static void WriteEnvironment(EnvironmentDto environment)
    {
        Console.WriteLine($"  {environment.Name}");
        Console.WriteLine($"    Active release: {environment.ActiveReleaseVersion ?? "(none)"}");
    }
}
