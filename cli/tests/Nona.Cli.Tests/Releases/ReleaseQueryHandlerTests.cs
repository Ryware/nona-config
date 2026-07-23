using System.Net;
using System.Text.Json;
using Nona.Cli.Releases.Queries;

namespace Nona.Cli.Tests.Releases;

[NotInParallel]
public sealed class ReleaseQueryHandlerTests
{
    [Test]
    public async Task List_HumanOutputSortsVersionsAndShowsReleaseMetadata()
    {
        var factory = ReleaseTestSupport.CreateHttpFactory(request =>
        {
            EnsureRequest(
                request,
                HttpMethod.Get,
                "/admin/projects/my-project/environments/production/releases");
            return Task.FromResult(
                ReleaseTestSupport.JsonResponse(
                    HttpStatusCode.OK,
                    """
                    [
                      {
                        "project":"my-project","environment":"production",
                        "version":"1.2.0","entryCount":2,"isActive":true,
                        "createdAt":"2024-01-02T00:00:00Z","actor":"alice"
                      },
                      {
                        "project":"my-project","environment":"production",
                        "version":"2.0.0","entryCount":1,"isActive":false,
                        "createdAt":"2024-01-03T00:00:00Z","actor":"bob"
                      }
                    ]
                    """));
        });

        var (exitCode, output, error) = await ReleaseTestSupport.CaptureOutputAsync(() =>
            new ListReleasesQueryHandler(factory).HandleAsync(
                new ListReleasesQuery(
                    ReleaseTestSupport.Connection,
                    "my-project",
                    "production"),
                CancellationToken.None));

        await Assert.That(exitCode).IsEqualTo(CliExitCodes.Success);
        await Assert.That(error).IsEmpty();
        await Assert.That(output).Contains("Releases — my-project / production");
        await Assert.That(output.IndexOf("2.0.0", StringComparison.Ordinal))
            .IsLessThan(output.IndexOf("1.2.0", StringComparison.Ordinal));
        await Assert.That(output).Contains("1.2.0 (active)");
        await Assert.That(output).Contains("Entries: 2");
        await Assert.That(output).Contains("Actor:   alice");
    }

    [Test]
    public async Task List_JsonOutputIsOneParseableArrayWithoutProse()
    {
        var factory = ReleaseTestSupport.CreateHttpFactory(_ => Task.FromResult(
            ReleaseTestSupport.JsonResponse(
                HttpStatusCode.OK,
                ReleaseTestSupport.ReleaseListJson)));

        var (exitCode, output, error) = await ReleaseTestSupport.CaptureOutputAsync(() =>
            new ListReleasesQueryHandler(factory).HandleAsync(
                new ListReleasesQuery(
                    ReleaseTestSupport.Connection,
                    "my-project",
                    "production",
                    Json: true),
                CancellationToken.None));

        using var document = JsonDocument.Parse(output);
        await Assert.That(exitCode).IsEqualTo(CliExitCodes.Success);
        await Assert.That(error).IsEmpty();
        await Assert.That(document.RootElement.ValueKind).IsEqualTo(JsonValueKind.Array);
        await Assert.That(document.RootElement.GetArrayLength()).IsEqualTo(2);
        await Assert.That(document.RootElement[0].GetProperty("version").GetString())
            .IsEqualTo("1.2.0");
        await Assert.That(document.RootElement[0].GetProperty("entryCount").GetInt32())
            .IsEqualTo(2);
        await Assert.That(output).DoesNotContain("Releases —");
    }

    [Test]
    public async Task List_EmptyJsonOutputIsAnEmptyArray()
    {
        var factory = ReleaseTestSupport.CreateHttpFactory(_ => Task.FromResult(
            ReleaseTestSupport.JsonResponse(HttpStatusCode.OK, "[]")));

        var (_, output, error) = await ReleaseTestSupport.CaptureOutputAsync(() =>
            new ListReleasesQueryHandler(factory).HandleAsync(
                new ListReleasesQuery(
                    ReleaseTestSupport.Connection,
                    "my-project",
                    "production",
                    Json: true),
                CancellationToken.None));

        using var document = JsonDocument.Parse(output);
        await Assert.That(error).IsEmpty();
        await Assert.That(document.RootElement.GetArrayLength()).IsEqualTo(0);
    }

    [Test]
    public async Task View_RequestsExactVersionAndRendersEntries()
    {
        var factory = ReleaseTestSupport.CreateHttpFactory(request =>
        {
            EnsureRequest(
                request,
                HttpMethod.Get,
                "/admin/projects/my-project/environments/production/releases/1.2.0");
            return Task.FromResult(
                ReleaseTestSupport.JsonResponse(
                    HttpStatusCode.OK,
                    ReleaseTestSupport.ReleaseDetailsJson));
        });

        var (exitCode, output, error) = await ReleaseTestSupport.CaptureOutputAsync(() =>
            new ViewReleaseQueryHandler(factory).HandleAsync(
                new ViewReleaseQuery(
                    ReleaseTestSupport.Connection,
                    "my-project",
                    "production",
                    "1.2.0"),
                CancellationToken.None));

        await Assert.That(exitCode).IsEqualTo(CliExitCodes.Success);
        await Assert.That(error).IsEmpty();
        await Assert.That(output).Contains("Release 1.2.0 — my-project / production");
        await Assert.That(output).Contains("Active:  yes");
        await Assert.That(output).Contains("feature.checkout = true");
        await Assert.That(output).Contains("Type: boolean; Scope: all");
    }

    [Test]
    public async Task View_JsonOutputIsOneParseableDetailsObjectWithoutProse()
    {
        var factory = ReleaseTestSupport.CreateHttpFactory(_ => Task.FromResult(
            ReleaseTestSupport.JsonResponse(
                HttpStatusCode.OK,
                ReleaseTestSupport.ReleaseDetailsJson)));

        var (exitCode, output, error) = await ReleaseTestSupport.CaptureOutputAsync(() =>
            new ViewReleaseQueryHandler(factory).HandleAsync(
                new ViewReleaseQuery(
                    ReleaseTestSupport.Connection,
                    "my-project",
                    "production",
                    "1.2.0",
                    Json: true),
                CancellationToken.None));

        using var document = JsonDocument.Parse(output);
        await Assert.That(exitCode).IsEqualTo(CliExitCodes.Success);
        await Assert.That(error).IsEmpty();
        await Assert.That(document.RootElement.ValueKind).IsEqualTo(JsonValueKind.Object);
        await Assert.That(document.RootElement.GetProperty("version").GetString())
            .IsEqualTo("1.2.0");
        await Assert.That(document.RootElement.GetProperty("entries").GetArrayLength())
            .IsEqualTo(2);
        await Assert.That(output).DoesNotContain("Release 1.2.0 —");
    }

    private static void EnsureRequest(
        HttpRequestMessage request,
        HttpMethod method,
        string path)
    {
        if (request.Method != method || request.RequestUri?.AbsolutePath != path)
        {
            throw new InvalidOperationException(
                $"Unexpected request: {request.Method} {request.RequestUri}");
        }
    }
}
