using System.Net;
using System.Text.Json;
using Nona.Cli.Releases.Commands;

namespace Nona.Cli.Tests.Releases;

[NotInParallel]
public sealed class CreateReleaseCommandHandlerTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Create_SendsNormalizedVersionWithoutEntriesPayload(bool activate)
    {
        string? body = null;
        var factory = ReleaseTestSupport.CreateHttpFactory(async request =>
        {
            if (request.Method != HttpMethod.Post ||
                request.RequestUri?.AbsolutePath !=
                "/admin/projects/my-project/environments/production/releases")
            {
                throw new InvalidOperationException(
                    $"Unexpected request: {request.Method} {request.RequestUri}");
            }

            body = await request.Content!.ReadAsStringAsync();
            return ReleaseTestSupport.JsonResponse(
                HttpStatusCode.Created,
                ReleaseTestSupport.ReleaseDetailsJson);
        });

        var (exitCode, output, error) = await ReleaseTestSupport.CaptureOutputAsync(() =>
            new CreateReleaseCommandHandler(factory).HandleAsync(
                new CreateReleaseCommand(
                    ReleaseTestSupport.Connection,
                    "my-project",
                    "production",
                    "1.2.0",
                    activate),
                CancellationToken.None));

        using var document = JsonDocument.Parse(body!);
        await Assert.That(exitCode).IsEqualTo(CliExitCodes.Success);
        await Assert.That(error).IsEmpty();
        await Assert.That(output).Contains("Created release: 1.2.0");
        await Assert.That(document.RootElement.GetProperty("version").GetString())
            .IsEqualTo("1.2.0");
        await Assert.That(document.RootElement.GetProperty("makeActive").GetBoolean())
            .IsEqualTo(activate);
        await Assert.That(document.RootElement.TryGetProperty("entries", out _)).IsFalse();
    }
}
