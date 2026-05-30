namespace Nona.Cli.Config.Queries;

internal sealed record ShowDefaultsQuery;

internal sealed class ShowDefaultsQueryHandler(CliDefaultsStore defaultsStore)
{
    public Task<int> HandleAsync(ShowDefaultsQuery query, CancellationToken ct)
    {
        var defaults = defaultsStore.Load();
        Console.WriteLine("Nona CLI Configuration defaults");
        Console.WriteLine($"Base URL: {defaults.BaseUrl ?? "(not set)"}");
        Console.WriteLine($"Project:  {defaults.Project ?? "(not set)"}");
        return Task.FromResult(0);
    }
}
