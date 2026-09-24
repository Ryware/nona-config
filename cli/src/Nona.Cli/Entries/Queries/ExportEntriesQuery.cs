using System.Text;
using Nona.Cli.Generated.Models;
using Nona.Cli.Releases;

namespace Nona.Cli.Entries.Queries;

internal sealed record ExportEntriesQuery(
    NonaCliConnectionOptions Connection,
    string Project,
    string Environment,
    string? Prefix = null,
    string Format = "dotenv",
    string? OutputFile = null,
    bool UseReleases = false,
    string? ReleaseVersion = null);

internal sealed class ExportEntriesQueryHandler(Func<HttpClient>? httpClientFactory = null)
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public async Task<int> HandleAsync(ExportEntriesQuery query, CancellationToken ct)
    {
        using var api = NonaClientFactory.Create(query.Connection, httpClientFactory);

        List<ConfigEntryDto> entries;
        if (query.UseReleases)
        {
            if (query.ReleaseVersion is not null && !ReleaseVersions.TryParseExact(query.ReleaseVersion, out _))
            {
                Console.Error.WriteLine("--release-version must be an exact major.minor.patch version, for example 1.2.3.");
                return CliExitCodes.ValidationError;
            }

            var result = await AdminReleaseEntryReader.ReadAsync(api, query.Project, query.Environment, query.ReleaseVersion, ct);
            if (!result.Success)
            {
                Console.Error.WriteLine(result.Error);
                return CliExitCodes.NotFound;
            }

            entries = (result.Entries ?? [])
                .Where(entry => query.Prefix is null || (entry.Key?.StartsWith(query.Prefix, StringComparison.Ordinal) ?? false))
                .Select(entry => entry.ToConfigEntryDto(query.Project, query.Environment))
                .ToList();
        }
        else
        {
            entries = await api.Admin.Projects[query.Project]
                .Environments[query.Environment].ConfigEntries.GetAsync(
                    request => request.QueryParameters.Prefix = query.Prefix,
                    cancellationToken: ct) ?? [];
        }

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
