using Nona.Cli.Generated.Models;

namespace Nona.Cli.Entries.Queries;

internal sealed record ListEntryShareLinksQuery(
    NonaCliConnectionOptions Connection,
    string Project,
    string Environment,
    string Key);

internal sealed class ListEntryShareLinksQueryHandler(Func<HttpClient>? httpClientFactory = null)
{
    public async Task<int> HandleAsync(ListEntryShareLinksQuery query, CancellationToken ct)
    {
        using var api = NonaClientFactory.Create(query.Connection, httpClientFactory);
        var shareLinks = await api.Admin.Projects[query.Project]
            .Environments[query.Environment].ConfigEntries[query.Key]
            .ShareLinks.GetAsync(cancellationToken: ct);

        if (shareLinks is null || shareLinks.Count == 0)
        {
            Console.WriteLine($"No share links found for [{query.Environment}] {query.Key}.");
            return 0;
        }

        Console.WriteLine($"Share links — {query.Project} / {query.Environment} / {query.Key}");
        foreach (var shareLink in shareLinks.OrderByDescending(link => link.CreatedAt))
            WriteShareLink(shareLink);

        return 0;
    }

    internal static void WriteShareLink(ParameterShareLinkDto shareLink)
    {
        Console.WriteLine($"  {CliUntypedNode.FormatInt64(shareLink.Id)}");
        Console.WriteLine($"    Access:      {(shareLink.CanEdit == true ? "edit" : "view")}");
        Console.WriteLine($"    Status:      {GetStatus(shareLink)}");
        Console.WriteLine($"    Created By:  {shareLink.CreatedBy}");
        Console.WriteLine($"    Created:     {shareLink.CreatedAt:O}");
        Console.WriteLine($"    Expires:     {shareLink.ExpiresAt:O}");
    }

    private static string GetStatus(ParameterShareLinkDto shareLink)
    {
        if (shareLink.RevokedAt is not null)
            return "revoked";

        if (shareLink.ExpiresAt is { } expiresAt && expiresAt <= DateTimeOffset.UtcNow)
            return "expired";

        return "active";
    }
}
