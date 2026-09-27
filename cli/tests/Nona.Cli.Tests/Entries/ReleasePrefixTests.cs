using System.Net;
using System.Text;
using Nona.Cli.Entries.Queries;
using static Nona.Cli.Tests.TestHelpers;

#pragma warning disable TUnit0055

namespace Nona.Cli.Tests.Entries;

[NotInParallel]
public sealed class ReleasePrefixTests
{
    private static readonly NonaCliConnectionOptions Connection = new("http://nona.test", "test-token");

    private const string ReleaseJson = """
        {
          "project":"my-project","environment":"production","version":"1.2.0",
          "entryCount":7,"isActive":true,"createdAt":"2024-01-01T00:00:00Z","actor":"alice",
          "entries":[
            {"key":"groupa:Two","value":"2","contentType":"text","scope":"all"},
            {"key":"GroupX:One","value":"3","contentType":"text","scope":"all"},
            {"key":"GroupA:One","value":"1","contentType":"text","scope":"all"},
            {"key":"Group_Three","value":"4","contentType":"text","scope":"all"},
            {"key":"GroupXThree","value":"5","contentType":"text","scope":"all"},
            {"key":"Group.One","value":"6","contentType":"text","scope":"all"},
            {"key":"Group-One","value":"7","contentType":"text","scope":"all"}
          ]
        }
        """;

    [Test]
    [Arguments("groupa:", false, "GroupA:One|groupa:Two")]
    [Arguments("GROUPA:", true, "GroupA:One|groupa:Two")]
    [Arguments("gRoUpA:", false, "GroupA:One|groupa:Two")]
    [Arguments("group_", true, "Group_Three")]
    [Arguments("group.", false, "Group.One")]
    [Arguments("group-", true, "Group-One")]
    [Arguments("missing:", false, "")]
    [Arguments("GroupA:OneLonger", true, "")]
    [Arguments(null, false, "Group-One|Group.One|GroupA:One|groupa:Two|GroupX:One|GroupXThree|Group_Three")]
    [Arguments("", true, "Group-One|Group.One|GroupA:One|groupa:Two|GroupX:One|GroupXThree|Group_Three")]
    public async Task List_MatchesAsciiPrefixes(string? prefix, bool activeRelease, string expectedKeys)
    {
        var (result, output) = await CaptureOutputAsync(() =>
            new ListEntriesQueryHandler(ReleaseHttp(activeRelease, ReleaseJson)).HandleAsync(
                new ListEntriesQuery(Connection, "my-project", "production", prefix, true,
                    activeRelease ? null : "1.2.0"), CancellationToken.None));

        var keys = output.Split(Environment.NewLine)
            .Where(line => line.StartsWith("  ", StringComparison.Ordinal)
                           && !line.StartsWith("    ", StringComparison.Ordinal))
            .Select(line => line[2..]);

        await Assert.That(result).IsEqualTo(CliExitCodes.Success);
        await Assert.That(string.Join('|', keys)).IsEqualTo(expectedKeys);
    }

    [Test]
    [Arguments("groupa:", false, "GroupA__One=1\ngroupa__Two=2\n")]
    [Arguments("GROUPA:", true, "GroupA__One=1\ngroupa__Two=2\n")]
    [Arguments("gRoUpA:", false, "GroupA__One=1\ngroupa__Two=2\n")]
    [Arguments("group_", true, "Group_Three=4\n")]
    [Arguments("group.", false, "Group.One=6\n")]
    [Arguments("group-", true, "Group-One=7\n")]
    [Arguments("missing:", false, "")]
    [Arguments("GroupA:OneLonger", true, "")]
    [Arguments(null, false, "Group-One=7\nGroup.One=6\nGroupA__One=1\ngroupa__Two=2\nGroupX__One=3\nGroupXThree=5\nGroup_Three=4\n")]
    [Arguments("", true, "Group-One=7\nGroup.One=6\nGroupA__One=1\ngroupa__Two=2\nGroupX__One=3\nGroupXThree=5\nGroup_Three=4\n")]
    public async Task Export_MatchesAsciiPrefixes(string? prefix, bool activeRelease, string expectedOutput)
    {
        var (result, output) = await CaptureOutputAsync(() =>
            new ExportEntriesQueryHandler(ReleaseHttp(activeRelease, ReleaseJson)).HandleAsync(
                new ExportEntriesQuery(Connection, "my-project", "production", prefix, UseReleases: true,
                    ReleaseVersion: activeRelease ? null : "1.2.0"), CancellationToken.None));

        await Assert.That(result).IsEqualTo(CliExitCodes.Success);
        await Assert.That(output).IsEqualTo(expectedOutput);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Export_WritesCaseInsensitiveMatchesToFile(bool activeRelease)
    {
        using var file = new TempFile();
        var (result, _) = await CaptureOutputAsync(() =>
            new ExportEntriesQueryHandler(ReleaseHttp(activeRelease, ReleaseJson)).HandleAsync(
                new ExportEntriesQuery(Connection, "my-project", "production", "GROUPA:", OutputFile: file.Path,
                    UseReleases: true, ReleaseVersion: activeRelease ? null : "1.2.0"), CancellationToken.None));

        await Assert.That(result).IsEqualTo(CliExitCodes.Success);
        var bytes = await File.ReadAllBytesAsync(file.Path);
        await Assert.That(bytes.SequenceEqual(Encoding.UTF8.GetBytes("GroupA__One=1\ngroupa__Two=2\n"))).IsTrue();
    }

    [Test]
    [Arguments("Ångström", "ång", false)]
    [Arguments("ſ:Legacy", "S:", false)]
    [Arguments("Ångström", "Ång", true)]
    [Arguments(null, null, true)]
    [Arguments(null, "", false)]
    [Arguments(null, "group", false)]
    public async Task ReleaseFilters_PreserveUnicodeAndNullKeyBehavior(string? key, string? prefix, bool matches)
    {
        var release = System.Text.Json.JsonSerializer.Serialize(new
        {
            project = "my-project",
            environment = "production",
            version = "1.2.0",
            entryCount = 1,
            isActive = true,
            createdAt = "2024-01-01T00:00:00Z",
            actor = "alice",
            entries = new[] { new { key, value = "value", contentType = "text", scope = "all" } }
        });
        var (listResult, listOutput) = await CaptureOutputAsync(() =>
            new ListEntriesQueryHandler(ReleaseHttp(false, release)).HandleAsync(
                new ListEntriesQuery(Connection, "my-project", "production", prefix, true, "1.2.0"),
                CancellationToken.None));
        var (exportResult, exportOutput) = await CaptureOutputAsync(() =>
            new ExportEntriesQueryHandler(ReleaseHttp(false, release)).HandleAsync(
                new ExportEntriesQuery(Connection, "my-project", "production", prefix, UseReleases: true,
                    ReleaseVersion: "1.2.0"), CancellationToken.None));

        await Assert.That(listResult).IsEqualTo(CliExitCodes.Success);
        await Assert.That(exportResult).IsEqualTo(CliExitCodes.Success);
        await Assert.That(listOutput.Contains("    Value:        value", StringComparison.Ordinal)).IsEqualTo(matches);
        await Assert.That(exportOutput).IsEqualTo(matches ? $"{key}=value\n" : string.Empty);
    }

    private static Func<HttpClient> ReleaseHttp(bool activeRelease, string release)
        => () => new HttpClient(new ReleaseHandler(activeRelease, release));

    private sealed class ReleaseHandler(bool activeRelease, string release) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            var body = path switch
            {
                "/admin/projects/my-project/environments" when activeRelease =>
                    """[{"name":"production","project":"my-project","activeReleaseVersion":"1.2.0"}]""",
                "/admin/projects/my-project/environments/production/releases/1.2.0" => release,
                _ => throw new InvalidOperationException($"Unexpected request: {request.RequestUri}")
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }

    private static async Task<(int Result, string Output)> CaptureOutputAsync(Func<Task<int>> action)
    {
        var previousOut = Console.Out;
        using var output = new StringWriter();
        try
        {
            Console.SetOut(output);
            return (await action(), output.ToString());
        }
        finally
        {
            Console.SetOut(previousOut);
        }
    }
}
