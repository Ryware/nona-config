using Nona.Cli.Generated.Models;

namespace Nona.Cli.Entries.Commands;

internal sealed record CreateEntryShareLinkCommand(
    NonaCliConnectionOptions Connection,
    string Project,
    string Environment,
    string Key,
    string Expiration,
    bool CanEdit,
    string? ShareBaseUrl);

internal sealed class CreateEntryShareLinkCommandHandler(Func<HttpClient>? httpClientFactory = null)
{
    public async Task<int> HandleAsync(CreateEntryShareLinkCommand command, CancellationToken ct)
    {
        using var api = NonaClientFactory.Create(command.Connection, httpClientFactory);
        var shareLink = await api.Admin.Projects[command.Project]
            .Environments[command.Environment].ConfigEntries[command.Key]
            .ShareLinks.PostAsync(new CreateParameterShareLinkRequest
            {
                Expiration = command.Expiration,
                CanEdit = command.CanEdit
            }, cancellationToken: ct);

        if (shareLink is null || string.IsNullOrWhiteSpace(shareLink.Token))
        {
            Console.Error.WriteLine("Failed to create share link.");
            return 1;
        }

        Console.WriteLine($"Created share link for [{command.Environment}] {command.Key}");
        Console.WriteLine($"  Id:       {CliUntypedNode.FormatInt64(shareLink.Id)}");
        Console.WriteLine($"  Access:   {(shareLink.CanEdit == true ? "edit" : "view")}");
        Console.WriteLine($"  Expires:  {shareLink.ExpiresAt:O}");
        Console.WriteLine($"  Token:    {shareLink.Token}");
        Console.WriteLine($"  Link:     {BuildShareUrl(command.ShareBaseUrl ?? command.Connection.BaseUrl, shareLink.Token)}");
        return 0;
    }

    private static string BuildShareUrl(string baseUrl, string token)
        => $"{baseUrl.TrimEnd('/')}/share/{Uri.EscapeDataString(token)}";
}
