using System.Text;

namespace Nona.Cli.Entries.Queries;

internal sealed record ExportEntriesQuery(
    NonaCliConnectionOptions Connection,
    string Project,
    string Environment,
    string? Prefix = null,
    string Format = "dotenv",
    string? OutputFile = null);

internal sealed class ExportEntriesQueryHandler(Func<HttpClient>? httpClientFactory = null)
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public async Task<int> HandleAsync(ExportEntriesQuery query, CancellationToken ct)
    {
        using var api = NonaClientFactory.Create(query.Connection, httpClientFactory);
        var entries = await api.Admin.Projects[query.Project]
            .Environments[query.Environment].ConfigEntries.GetAsync(
                request => request.QueryParameters.Prefix = query.Prefix,
                cancellationToken: ct);

        entries ??= [];
        var content = DotEnvEntryFormatter.Format(entries);

        if (string.IsNullOrWhiteSpace(query.OutputFile))
        {
            Console.Out.Write(content);
            if (content.Length > 0)
                Console.Out.Write('\n');
            return CliExitCodes.Success;
        }

        await File.WriteAllTextAsync(query.OutputFile, content.Length > 0 ? content + "\n" : content, Utf8NoBom, ct);
        Console.WriteLine($"Wrote {entries.Count} entries to {query.OutputFile}");
        return CliExitCodes.Success;
    }
}
