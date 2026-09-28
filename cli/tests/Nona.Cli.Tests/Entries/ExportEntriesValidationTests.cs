using System.Net;
using System.Text;
using System.Text.Json;
using Nona.Cli.Entries.Queries;
using static Nona.Cli.Tests.TestHelpers;

#pragma warning disable TUnit0055

namespace Nona.Cli.Tests.Entries;

[NotInParallel]
public sealed class ExportEntriesValidationTests
{
    [Test]
    [Arguments("working", "stdout")]
    [Arguments("working", "existing")]
    [Arguments("working", "missing")]
    [Arguments("exact", "stdout")]
    [Arguments("exact", "existing")]
    [Arguments("exact", "missing")]
    [Arguments("active", "stdout")]
    [Arguments("active", "existing")]
    [Arguments("active", "missing")]
    public async Task Export_RejectsInvalidEntriesBeforeWriting(string source, string destination)
    {
        (string? Key, string Value)[] invalidEntries = [
            (null, "hidden"), ("", "hidden"), ("__proto__", "hidden"),
            ("A=B", "hidden"), ("A\nB", "hidden"), ("A\rB", "hidden"),
            (" K", "hidden"), ("K ", "hidden"), ("\tK", "hidden"), ("K\t", "hidden"),
            ("#K", "hidden"), ("export K", "hidden"),
            ("Secret", "hidden\r"), ("Secret", "hidden'\"`#"), ("Secret", "hidden'`\\n#")
        ];
        foreach (var (key, value) in invalidEntries)
        {
            using var file = new TempFile();
            byte[] original = [0xEF, 0xBB, 0xBF, 0xFF, 0x00, 0x0D, 0x0A];
            if (destination == "missing") File.Delete(file.Path);
            else await File.WriteAllBytesAsync(file.Path, original);
            var entries = JsonSerializer.Serialize(new[] {
                new { key, value, contentType = "text", scope = "all" }
            });
            var (code, output, error) = await ExportAsync(source, entries,
                destination == "stdout" ? null : file.Path);

            await Assert.That(code).IsEqualTo(CliExitCodes.ValidationError);
            await Assert.That(output).IsEmpty();
            await Assert.That(error).Contains("Cannot export dotenv");
            await Assert.That(error.Contains("hidden", StringComparison.Ordinal)).IsFalse();
            if (key == "Secret") await Assert.That(error).Contains("Secret");
            if (destination == "missing") await Assert.That(File.Exists(file.Path)).IsFalse();
            else await Assert.That((await File.ReadAllBytesAsync(file.Path)).SequenceEqual(original)).IsTrue();
        }
    }

    [Test]
    [Arguments("working")]
    [Arguments("exact")]
    [Arguments("active")]
    public async Task Export_ValidatesOnlySelectedEntries(string source)
    {
        const string entries = """
            [{"key":"Safe:Key","value":"ok","contentType":"text","scope":"all"},
             {"key":"__proto__","value":"hidden\r","contentType":"text","scope":"all"}]
            """;
        var (code, output, error) = await ExportAsync(source, entries, prefix: "Safe:");
        await Assert.That(code).IsEqualTo(CliExitCodes.Success);
        await Assert.That(output).IsEqualTo("Safe__Key=ok\n");
        await Assert.That(error).IsEmpty();
    }

    internal static async Task<(int Code, string Output, string Error)> ExportAsync(
        string source, string entries, string? file = null, string? prefix = null)
    {
        var previousOut = Console.Out;
        var previousError = Console.Error;
        using var output = new StringWriter();
        using var error = new StringWriter();
        try
        {
            Console.SetOut(output);
            Console.SetError(error);
            var handler = new ExportEntriesQueryHandler(() => new HttpClient(new ExportHandler(source, entries)));
            var code = await handler.HandleAsync(new(
                new("http://nona.test", "test-token"), "my-project", "production", prefix,
                OutputFile: file, UseReleases: source != "working",
                ReleaseVersion: source == "exact" ? "1.2.0" : null), CancellationToken.None);
            return (code, output.ToString(), error.ToString());
        }
        finally
        {
            Console.SetOut(previousOut);
            Console.SetError(previousError);
        }
    }

    private sealed class ExportHandler(string source, string entries) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var uri = request.RequestUri!;
            string body;
            if (uri.AbsolutePath == "/admin/projects/my-project/environments" && source == "active")
                body = """[{"name":"production","project":"my-project","activeReleaseVersion":"1.2.0"}]""";
            else if (uri.AbsolutePath == "/admin/projects/my-project/environments/production/releases/1.2.0"
                     && source != "working")
                body = $$"""
                    {"project":"my-project","environment":"production","version":"1.2.0",
                     "isActive":true,"createdAt":"2024-01-01T00:00:00Z","actor":"alice","entries":{{entries}}}
                    """;
            else if (uri.AbsolutePath == "/admin/projects/my-project/environments/production/config-entries"
                     && source == "working")
            {
                if (uri.Query == "?prefix=Safe%3A")
                    body = """[{"key":"Safe:Key","value":"ok","contentType":"text","scope":"all"}]""";
                else if (uri.Query.Length == 0) body = entries;
                else throw new InvalidOperationException($"Unexpected query: {uri}");
            }
            else throw new InvalidOperationException($"Unexpected request: {uri}");

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}
