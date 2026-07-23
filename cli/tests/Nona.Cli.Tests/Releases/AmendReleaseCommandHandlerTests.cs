using System.Net;
using System.Text.Json;
using Nona.Cli.Generated.Models;
using Nona.Cli.Releases;
using Nona.Cli.Releases.Commands;

#pragma warning disable TUnit0055

namespace Nona.Cli.Tests.Releases;

[NotInParallel]
public sealed class AmendReleaseCommandHandlerTests
{
    [Test]
    public async Task DirectAmend_CalculatesNextPatchAndPublishesEditedCopy()
    {
        var requests = new List<string>();
        string? postedBody = null;
        var factory = ReleaseTestSupport.CreateHttpFactory(async request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            requests.Add($"{request.Method} {path}");
            if (request.Method == HttpMethod.Get &&
                path == "/admin/projects/my-project/environments/production/releases")
            {
                return ReleaseTestSupport.JsonResponse(
                    HttpStatusCode.OK,
                    """
                    [
                      {"version":"1.2.0"},
                      {"version":"1.2.1"},
                      {"version":"1.2.3"},
                      {"version":"1.1.99"},
                      {"version":"2.2.50"}
                    ]
                    """);
            }

            if (request.Method == HttpMethod.Get &&
                path == "/admin/projects/my-project/environments/production/releases/1.2.0")
            {
                return ReleaseTestSupport.JsonResponse(
                    HttpStatusCode.OK,
                    SourceReleaseJson(
                        "1.2.0",
                        """
                        [
                          {"key":"feature.checkout","value":"true","contentType":"boolean","scope":"client"},
                          {"key":"deprecated.key","value":"old","contentType":"text","scope":"all"}
                        ]
                        """));
            }

            if (request.Method == HttpMethod.Post &&
                path == "/admin/projects/my-project/environments/production/releases")
            {
                postedBody = await request.Content!.ReadAsStringAsync();
                return ReleaseTestSupport.JsonResponse(
                    HttpStatusCode.Created,
                    SourceReleaseJson(
                        "1.2.4",
                        """
                        [
                          {"key":"feature.checkout","value":"false","contentType":"boolean","scope":"client"},
                          {"key":"new.number","value":"42","contentType":"number","scope":"all"}
                        ]
                        """));
            }

            throw new InvalidOperationException(
                $"Unexpected request: {request.Method} {request.RequestUri}");
        });

        var (exitCode, output, error) = await ReleaseTestSupport.CaptureOutputAsync(() =>
            new AmendReleaseCommandHandler(factory).HandleAsync(
                Command(
                    "1.2.0",
                    sets: ["FEATURE.CHECKOUT=false", "new.number=42"],
                    deletes: ["deprecated.key"]),
                CancellationToken.None));

        using var posted = JsonDocument.Parse(postedBody!);
        var entries = posted.RootElement.GetProperty("entries");
        await Assert.That(exitCode).IsEqualTo(CliExitCodes.Success);
        await Assert.That(error).IsEmpty();
        await Assert.That(output).Contains("Published amended release 1.2.4 from 1.2.0.");
        await Assert.That(posted.RootElement.GetProperty("version").GetString())
            .IsEqualTo("1.2.4");
        await Assert.That(posted.RootElement.GetProperty("makeActive").GetBoolean()).IsFalse();
        await Assert.That(entries.GetArrayLength()).IsEqualTo(2);
        await Assert.That(entries[0].GetProperty("value").GetString()).IsEqualTo("false");
        await Assert.That(entries[0].GetProperty("contentType").GetString())
            .IsEqualTo("boolean");
        await Assert.That(entries[0].GetProperty("scope").GetString()).IsEqualTo("client");
        await Assert.That(entries[1].GetProperty("contentType").GetString())
            .IsEqualTo("number");
        await Assert.That(requests[0]).EndsWith("/releases");
        await Assert.That(requests[1]).EndsWith("/releases/1.2.0");
        await Assert.That(requests.All(request => !request.Contains("config-entries")))
            .IsTrue();
    }

    [Test]
    public async Task Amend_UsesOnlyTheSourceReleaseLineForNextPatch()
    {
        string? postedBody = null;
        var factory = StandardAmendFactory(
            listJson: """
                [
                  {"version":"1.1.0"},
                  {"version":"1.1.2"},
                  {"version":"1.2.50"}
                ]
                """,
            sourceVersion: "1.1.0",
            sourceEntriesJson:
                """[{"key":"one","value":"1","contentType":"number","scope":"all"}]""",
            onPost: body => postedBody = body,
            targetVersion: "1.1.3");

        var exitCode = await new AmendReleaseCommandHandler(factory).HandleAsync(
            Command("1.1.0", sets: ["one=2"]),
            CancellationToken.None);

        using var posted = JsonDocument.Parse(postedBody!);
        await Assert.That(exitCode).IsEqualTo(CliExitCodes.Success);
        await Assert.That(posted.RootElement.GetProperty("version").GetString())
            .IsEqualTo("1.1.3");
    }

    [Test]
    public async Task Amend_CanPublishAnExplicitEmptyEntriesArray()
    {
        string? postedBody = null;
        var factory = StandardAmendFactory(
            listJson: """[{"version":"1.0.0"}]""",
            sourceVersion: "1.0.0",
            sourceEntriesJson:
                """[{"key":"only.key","value":"value","contentType":"text","scope":"all"}]""",
            onPost: body => postedBody = body,
            targetVersion: "1.0.1");

        var exitCode = await new AmendReleaseCommandHandler(factory).HandleAsync(
            Command("1.0.0", deletes: ["only.key"]),
            CancellationToken.None);

        using var posted = JsonDocument.Parse(postedBody!);
        await Assert.That(exitCode).IsEqualTo(CliExitCodes.Success);
        await Assert.That(posted.RootElement.GetProperty("entries").ValueKind)
            .IsEqualTo(JsonValueKind.Array);
        await Assert.That(posted.RootElement.GetProperty("entries").GetArrayLength())
            .IsEqualTo(0);
    }

    [Test]
    public async Task Amend_FromFilePublishesExactlyTheFileEntries()
    {
        using var file = new TestHelpers.TempFile();
        const string fileEntries = """
            [
              {"key":"file.key","value":"a=b","contentType":"text","scope":"server"}
            ]
            """;
        await File.WriteAllTextAsync(file.Path, fileEntries);
        string? postedBody = null;
        var factory = StandardAmendFactory(
            listJson: """[{"version":"1.0.0"}]""",
            sourceVersion: "1.0.0",
            sourceEntriesJson: "[]",
            onPost: body => postedBody = body,
            targetVersion: "1.0.1");

        var exitCode = await new AmendReleaseCommandHandler(factory).HandleAsync(
            Command("1.0.0", fromFile: file.Path),
            CancellationToken.None);

        using var expected = JsonDocument.Parse(fileEntries);
        using var posted = JsonDocument.Parse(postedBody!);
        await Assert.That(exitCode).IsEqualTo(CliExitCodes.Success);
        await Assert.That(JsonElement.DeepEquals(
            expected.RootElement,
            posted.RootElement.GetProperty("entries"))).IsTrue();
    }

    [Test]
    public async Task Amend_WithoutModeIsValidationErrorAndMakesNoRequest()
    {
        var requestCount = 0;
        var factory = ReleaseTestSupport.CreateHttpFactory(_ =>
        {
            requestCount++;
            throw new InvalidOperationException("No request expected.");
        });

        var (exitCode, output, error) = await ReleaseTestSupport.CaptureOutputAsync(() =>
            new AmendReleaseCommandHandler(factory).HandleAsync(
                Command("1.0.0"),
                CancellationToken.None));

        await Assert.That(exitCode).IsEqualTo(CliExitCodes.ValidationError);
        await Assert.That(requestCount).IsEqualTo(0);
        await Assert.That(output).IsEmpty();
        await Assert.That(error).Contains("--set/--delete or --from-file");
    }

    [Test]
    public async Task Amend_RejectsMultipleEditModesBeforeAnyRequest()
    {
        var requestCount = 0;
        var factory = ReleaseTestSupport.CreateHttpFactory(_ =>
        {
            requestCount++;
            throw new InvalidOperationException("No request expected.");
        });

        var (exitCode, _, error) = await ReleaseTestSupport.CaptureOutputAsync(() =>
            new AmendReleaseCommandHandler(factory).HandleAsync(
                Command("1.0.0", sets: ["one=two"], fromFile: "entries.json"),
                CancellationToken.None));

        await Assert.That(exitCode).IsEqualTo(CliExitCodes.ValidationError);
        await Assert.That(requestCount).IsEqualTo(0);
        await Assert.That(error).Contains("Choose exactly one amend mode");
    }

    [Test]
    public async Task HandleAsync_PrintsEveryValidationErrorWithoutRetryingOrChangingEntries()
    {
        const string sourceEntriesJson = """
            [
              {"key":"legacy key","value":"kept","contentType":"text","scope":"all"},
              {"key":"DUPLICATE","value":"first","contentType":"text","scope":"client"},
              {"key":"duplicate","value":"second","contentType":"text","scope":"server"},
              {"key":"legacy.scope","value":"kept","contentType":"text","scope":"public"},
              {"key":"legacy.type","value":"kept","contentType":"xml","scope":"all"},
              {"key":"legacy.value","value":"not-a-number","contentType":"number","scope":"all"}
            ]
            """;
        const string validationProblemJson = """
            {
              "title":"One or more validation errors occurred.",
              "status":400,
              "detail":"One or more validation errors occurred.",
              "errors":{
                "Entries[0].Key":["Release entry keys may only contain letters, numbers, colons, underscores, periods, and hyphens."],
                "Entries[2].Key":["Release entry keys must be unique (case-insensitive)."],
                "Entries[3].Scope":["Invalid scope. Must be 'client', 'server', or 'all'."],
                "Entries[4].ContentType":["Content type must be one of: text, number, boolean, json."],
                "Entries[5].Value":["Value must be a valid number."]
              }
            }
            """;

        var postCount = 0;
        string? postedBody = null;
        var factory = ReleaseTestSupport.CreateHttpFactory(async request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Get && path.EndsWith("/releases"))
            {
                return ReleaseTestSupport.JsonResponse(
                    HttpStatusCode.OK,
                    """[{"version":"1.1.0"}]""");
            }

            if (request.Method == HttpMethod.Get && path.EndsWith("/releases/1.1.0"))
            {
                return ReleaseTestSupport.JsonResponse(
                    HttpStatusCode.OK,
                    SourceReleaseJson("1.1.0", sourceEntriesJson));
            }

            if (request.Method == HttpMethod.Post && path.EndsWith("/releases"))
            {
                postCount++;
                postedBody = await request.Content!.ReadAsStringAsync();
                return ReleaseTestSupport.ProblemResponse(
                    HttpStatusCode.BadRequest,
                    validationProblemJson);
            }

            throw new InvalidOperationException(
                $"Unexpected request: {request.Method} {request.RequestUri}");
        });

        var (exitCode, output, error) = await ReleaseTestSupport.CaptureOutputAsync(() =>
            new AmendReleaseCommandHandler(factory).HandleAsync(
                Command("1.1.0", sets: ["legacy.value=not-a-number"]),
                CancellationToken.None));

        await Assert.That(exitCode).IsEqualTo(CliExitCodes.ValidationError);
        await Assert.That(postCount).IsEqualTo(1);
        await Assert.That(output).IsEmpty();
        await Assert.That(error).Contains(
            "Entries[0].Key: Release entry keys may only contain letters, numbers, colons, underscores, periods, and hyphens.");
        await Assert.That(error).Contains(
            "Entries[2].Key: Release entry keys must be unique (case-insensitive).");
        await Assert.That(error).Contains(
            "Entries[3].Scope: Invalid scope. Must be 'client', 'server', or 'all'.");
        await Assert.That(error).Contains(
            "Entries[4].ContentType: Content type must be one of: text, number, boolean, json.");
        await Assert.That(error).Contains(
            "Entries[5].Value: Value must be a valid number.");

        using var sourceEntries = JsonDocument.Parse(sourceEntriesJson);
        using var posted = JsonDocument.Parse(postedBody!);
        await Assert.That(posted.RootElement.GetProperty("version").GetString())
            .IsEqualTo("1.1.1");
        await Assert.That(posted.RootElement.GetProperty("makeActive").GetBoolean()).IsFalse();
        await Assert.That(JsonElement.DeepEquals(
            posted.RootElement.GetProperty("entries"),
            sourceEntries.RootElement)).IsTrue();
    }

    [Test]
    public async Task Amend_ConflictReturnsExitFiveWithoutRetrying()
    {
        var postCount = 0;
        var factory = ReleaseTestSupport.CreateHttpFactory(async request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Get && path.EndsWith("/releases"))
            {
                return ReleaseTestSupport.JsonResponse(
                    HttpStatusCode.OK,
                    """[{"version":"1.0.0"}]""");
            }

            if (request.Method == HttpMethod.Get && path.EndsWith("/releases/1.0.0"))
            {
                return ReleaseTestSupport.JsonResponse(
                    HttpStatusCode.OK,
                    SourceReleaseJson(
                        "1.0.0",
                        """[{"key":"one","value":"1","contentType":"number","scope":"all"}]"""));
            }

            if (request.Method == HttpMethod.Post && path.EndsWith("/releases"))
            {
                postCount++;
                _ = await request.Content!.ReadAsStringAsync();
                return ReleaseTestSupport.ProblemResponse(
                    HttpStatusCode.Conflict,
                    """{"title":"Conflict","status":409,"detail":"Release already exists"}""");
            }

            throw new InvalidOperationException(
                $"Unexpected request: {request.Method} {request.RequestUri}");
        });

        var (exitCode, output, error) = await ReleaseTestSupport.CaptureOutputAsync(() =>
            new AmendReleaseCommandHandler(factory).HandleAsync(
                Command("1.0.0", sets: ["one=2"]),
                CancellationToken.None));

        await Assert.That(exitCode).IsEqualTo(CliExitCodes.Conflict);
        await Assert.That(postCount).IsEqualTo(1);
        await Assert.That(output).IsEmpty();
        await Assert.That(error).Contains("Release already exists");
    }

    [Test]
    public async Task Amend_CancellationPropagates()
    {
        var factory = ReleaseTestSupport.CreateHttpFactory(
            (_, cancellationToken) =>
                Task.FromCanceled<HttpResponseMessage>(cancellationToken));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        OperationCanceledException? exception = null;
        try
        {
            await new AmendReleaseCommandHandler(factory).HandleAsync(
                Command("1.0.0", sets: ["one=2"]),
                cancellation.Token);
        }
        catch (OperationCanceledException caught)
        {
            exception = caught;
        }

        await Assert.That(exception).IsNotNull();
    }

    private static AmendReleaseCommand Command(
        string sourceVersion,
        IReadOnlyList<string>? sets = null,
        IReadOnlyList<string>? deletes = null,
        string? fromFile = null)
        => new(
            ReleaseTestSupport.Connection,
            "my-project",
            "production",
            sourceVersion,
            sets ?? [],
            deletes ?? [],
            fromFile);

    private static Func<HttpClient> StandardAmendFactory(
        string listJson,
        string sourceVersion,
        string sourceEntriesJson,
        Action<string> onPost,
        string targetVersion)
        => ReleaseTestSupport.CreateHttpFactory(async request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Get && path.EndsWith("/releases"))
            {
                return ReleaseTestSupport.JsonResponse(HttpStatusCode.OK, listJson);
            }

            if (request.Method == HttpMethod.Get &&
                path.EndsWith($"/releases/{sourceVersion}"))
            {
                return ReleaseTestSupport.JsonResponse(
                    HttpStatusCode.OK,
                    SourceReleaseJson(sourceVersion, sourceEntriesJson));
            }

            if (request.Method == HttpMethod.Post && path.EndsWith("/releases"))
            {
                var body = await request.Content!.ReadAsStringAsync();
                onPost(body);
                using var posted = JsonDocument.Parse(body);
                return ReleaseTestSupport.JsonResponse(
                    HttpStatusCode.Created,
                    SourceReleaseJson(
                        targetVersion,
                        posted.RootElement
                            .GetProperty("entries").GetRawText()));
            }

            throw new InvalidOperationException(
                $"Unexpected request: {request.Method} {request.RequestUri}");
        });

    private static string SourceReleaseJson(string version, string entriesJson)
        => $$"""
            {
              "project":"my-project",
              "environment":"production",
              "version":"{{version}}",
              "entryCount":0,
              "isActive":false,
              "createdAt":"2024-01-01T00:00:00Z",
              "actor":"alice",
              "entries":{{entriesJson}}
            }
            """;
}
