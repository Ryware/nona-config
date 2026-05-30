namespace Nona.Cli.Auth.Queries;

internal sealed record WhoAmIQuery(CliAuthSession? Session);

internal sealed class WhoAmIQueryHandler()
{
    public Task<int> HandleAsync(WhoAmIQuery query, CancellationToken ct)
    {
        var session = query.Session;

        if (session is null)
        {
            Console.WriteLine("Not logged in.");
            return Task.FromResult(0);
        }

        Console.WriteLine("Current session");
        Console.WriteLine($"Username:   {session.Username}");
        Console.WriteLine($"Status:     {(session.IsExpired ? "expired" : "active")}");
        return Task.FromResult(0);
    }
}
