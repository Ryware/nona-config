using System.Net;
using Nona.Cli.Releases.Commands;

namespace Nona.Cli.Tests.Releases;

[NotInParallel]
public sealed class ReleaseLifecycleCommandHandlerTests
{
    [Test]
    public async Task Activate_SendsExactVersionToActiveReleaseEndpoint()
    {
        string? body = null;
        var factory = ReleaseTestSupport.CreateHttpFactory(async request =>
        {
            EnsureRequest(
                request,
                HttpMethod.Put,
                "/admin/projects/my-project/environments/production/active-release");
            body = await request.Content!.ReadAsStringAsync();
            return ReleaseTestSupport.JsonResponse(
                HttpStatusCode.OK,
                """
                {
                  "project":"my-project","name":"production",
                  "activeReleaseVersion":"1.2.1",
                  "createdAt":"2024-01-01T00:00:00Z",
                  "updatedAt":"2024-01-02T00:00:00Z"
                }
                """);
        });

        var (exitCode, output, error) = await ReleaseTestSupport.CaptureOutputAsync(() =>
            new ActivateReleaseCommandHandler(factory).HandleAsync(
                new ActivateReleaseCommand(
                    ReleaseTestSupport.Connection,
                    "my-project",
                    "production",
                    "1.2.1"),
                CancellationToken.None));

        await Assert.That(exitCode).IsEqualTo(CliExitCodes.Success);
        await Assert.That(error).IsEmpty();
        await Assert.That(body).Contains("\"version\":\"1.2.1\"");
        await Assert.That(output).Contains(
            "Active release for my-project / production: 1.2.1");
    }

    [Test]
    public async Task ClearActive_DeletesActiveReleaseEndpoint()
    {
        var factory = ReleaseTestSupport.CreateHttpFactory(request =>
        {
            EnsureRequest(
                request,
                HttpMethod.Delete,
                "/admin/projects/my-project/environments/production/active-release");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        });

        var (exitCode, output, error) = await ReleaseTestSupport.CaptureOutputAsync(() =>
            new ClearActiveReleaseCommandHandler(factory).HandleAsync(
                new ClearActiveReleaseCommand(
                    ReleaseTestSupport.Connection,
                    "my-project",
                    "production"),
                CancellationToken.None));

        await Assert.That(exitCode).IsEqualTo(CliExitCodes.Success);
        await Assert.That(error).IsEmpty();
        await Assert.That(output).Contains(
            "Cleared active release for my-project / production.");
    }

    [Test]
    public async Task Delete_DeletesExactReleaseEndpoint()
    {
        var factory = ReleaseTestSupport.CreateHttpFactory(request =>
        {
            EnsureRequest(
                request,
                HttpMethod.Delete,
                "/admin/projects/my-project/environments/production/releases/1.2.0");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        });

        var (exitCode, output, error) = await ReleaseTestSupport.CaptureOutputAsync(() =>
            new DeleteReleaseCommandHandler(factory).HandleAsync(
                new DeleteReleaseCommand(
                    ReleaseTestSupport.Connection,
                    "my-project",
                    "production",
                    "1.2.0"),
                CancellationToken.None));

        await Assert.That(exitCode).IsEqualTo(CliExitCodes.Success);
        await Assert.That(error).IsEmpty();
        await Assert.That(output).Contains("Deleted release: 1.2.0");
    }

    [Test]
    public async Task Delete_ActiveReleaseConflictRetainsBackendMessageAndExitMapping()
    {
        var factory = ReleaseTestSupport.CreateHttpFactory(_ => Task.FromResult(
            ReleaseTestSupport.ProblemResponse(
                HttpStatusCode.Conflict,
                """
                {
                  "title":"Conflict",
                  "status":409,
                  "detail":"Cannot delete active release 1.2.0. Clear or activate another release first."
                }
                """)));

        Exception? caught = null;
        try
        {
            await new DeleteReleaseCommandHandler(factory).HandleAsync(
                new DeleteReleaseCommand(
                    ReleaseTestSupport.Connection,
                    "my-project",
                    "production",
                    "1.2.0"),
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            caught = exception;
        }

        await Assert.That(caught).IsNotNull();
        var error = CliExceptionHandler.Describe(caught!);
        await Assert.That(error.ExitCode).IsEqualTo(CliExitCodes.Conflict);
        await Assert.That(error.Message).Contains(
            "Cannot delete active release 1.2.0. Clear or activate another release first.");
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
