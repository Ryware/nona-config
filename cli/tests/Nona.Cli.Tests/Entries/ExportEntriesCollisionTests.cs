using System.Net;
using System.Text;
using Nona.Cli.Entries.Queries;
using static Nona.Cli.Tests.TestHelpers;

#pragma warning disable TUnit0055

namespace Nona.Cli.Tests.Entries;

[NotInParallel]
public sealed class ExportEntriesCollisionTests
{
    private static readonly NonaCliConnectionOptions Connection = new("http://nona.test", "test-token");

    private const string EntriesJson = """
        [
          {"key":"A:Safe","value":"safe-value","contentType":"text","scope":"all"},
          {"key":"Group:Flag","value":"secret-one","contentType":"text","scope":"all"},
          {"key":"group__flag","value":"secret-two","contentType":"text","scope":"all"}
        ]
        """;

    private const string FilteredEntriesJson = """
        [{"key":"Group:Flag","value":"secret-one","contentType":"text","scope":"all"}]
        """;

    private const string ReleaseJson = $$"""
        {
          "project":"my-project","environment":"production","version":"1.2.0",
          "entryCount":3,"isActive":true,"createdAt":"2024-01-01T00:00:00Z","actor":"alice",
          "entries":{{EntriesJson}}
        }
        """;

    private static readonly string ExpectedError =
        "Cannot export dotenv: keys 'Group:Flag', 'group__flag' map to duplicate key 'Group__Flag'."
        + Environment.NewLine;

    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task Export_RejectsCollisionWithoutStdout(bool useReleases, bool activeRelease)
    {
        var (result, output, error) = await CaptureOutputAsync(() =>
            new ExportEntriesQueryHandler(ExportHttp()).HandleAsync(
                Query(useReleases, activeRelease), CancellationToken.None));

        await Assert.That(result).IsEqualTo(CliExitCodes.ValidationError);
        await Assert.That(output).IsEmpty();
        await Assert.That(error).IsEqualTo(ExpectedError);
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task Export_PreservesExistingFileOnCollision(bool useReleases, bool activeRelease)
    {
        using var file = new TempFile();
        byte[] original = [0xEF, 0xBB, 0xBF, 0xFF, 0x00, 0x0D, 0x0A];
        await File.WriteAllBytesAsync(file.Path, original);

        var (result, output, error) = await CaptureOutputAsync(() =>
            new ExportEntriesQueryHandler(ExportHttp()).HandleAsync(
                Query(useReleases, activeRelease, file.Path), CancellationToken.None));

        await Assert.That((await File.ReadAllBytesAsync(file.Path)).SequenceEqual(original)).IsTrue();
        await Assert.That(result).IsEqualTo(CliExitCodes.ValidationError);
        await Assert.That(output).IsEmpty();
        await Assert.That(error).IsEqualTo(ExpectedError);
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task Export_DoesNotCreateFileOnCollision(bool useReleases, bool activeRelease)
    {
        using var file = new TempFile();
        File.Delete(file.Path);

        var (result, output, error) = await CaptureOutputAsync(() =>
            new ExportEntriesQueryHandler(ExportHttp()).HandleAsync(
                Query(useReleases, activeRelease, file.Path), CancellationToken.None));

        await Assert.That(File.Exists(file.Path)).IsFalse();
        await Assert.That(result).IsEqualTo(CliExitCodes.ValidationError);
        await Assert.That(output).IsEmpty();
        await Assert.That(error).IsEqualTo(ExpectedError);
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task Export_SucceedsWhenPrefixExcludesConflictingKey(bool useReleases, bool activeRelease)
    {
        var (result, output, error) = await CaptureOutputAsync(() =>
            new ExportEntriesQueryHandler(ExportHttp()).HandleAsync(
                Query(useReleases, activeRelease, prefix: "Group:"), CancellationToken.None));

        await Assert.That(result).IsEqualTo(CliExitCodes.Success);
        await Assert.That(output).IsEqualTo("Group__Flag=secret-one\n");
        await Assert.That(error).IsEmpty();
    }

    private static ExportEntriesQuery Query(bool useReleases, bool activeRelease, string? outputFile = null, string? prefix = null)
        => new(Connection, "my-project", "production", prefix, OutputFile: outputFile,
            UseReleases: useReleases, ReleaseVersion: useReleases && !activeRelease ? "1.2.0" : null);

    private static Func<HttpClient> ExportHttp() => () => new HttpClient(new ExportHandler());

    private sealed class ExportHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var uri = request.RequestUri!;
            var body = uri.AbsolutePath switch
            {
                "/admin/projects/my-project/environments" =>
                    """[{"name":"production","project":"my-project","activeReleaseVersion":"1.2.0"}]""",
                "/admin/projects/my-project/environments/production/releases/1.2.0" => ReleaseJson,
                "/admin/projects/my-project/environments/production/config-entries" when uri.Query == "?prefix=Group%3A" => FilteredEntriesJson,
                "/admin/projects/my-project/environments/production/config-entries" when uri.Query.Length == 0 => EntriesJson,
                _ => throw new InvalidOperationException($"Unexpected request: {uri}")
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }

    private static async Task<(int Result, string Output, string Error)> CaptureOutputAsync(Func<Task<int>> action)
    {
        var previousOut = Console.Out;
        var previousError = Console.Error;
        using var output = new StringWriter();
        using var error = new StringWriter();
        try
        {
            Console.SetOut(output);
            Console.SetError(error);
            return (await action(), output.ToString(), error.ToString());
        }
        finally
        {
            Console.SetOut(previousOut);
            Console.SetError(previousError);
        }
    }
}
