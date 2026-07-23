using Nona.Cli.Generated.Models;
using Nona.Cli.Releases;

namespace Nona.Cli.Tests.Releases;

public sealed class ReleaseEntryEditingTests
{
    [Test]
    public async Task DirectEdits_AreCaseInsensitiveAndPreserveExistingMetadata()
    {
        var source = new List<ConfigReleaseEntryDto>
        {
            Entry("Feature.Checkout", "true", "boolean", "client"),
            Entry("api.url", "old", "text", "server"),
            Entry("deprecated.key", "remove", "text", "all")
        };

        var edited = ReleaseEntryEditing.ApplyDirectEdits(
            source,
            ["feature.checkout=false", "new.number=42", "new.value=a=b=c"],
            ["DEPRECATED.KEY"]);

        var checkout = edited.Single(entry => entry.Key == "Feature.Checkout");
        await Assert.That(checkout.Value).IsEqualTo("false");
        await Assert.That(checkout.ContentType).IsEqualTo("boolean");
        await Assert.That(checkout.Scope).IsEqualTo("client");
        await Assert.That(edited.Single(entry => entry.Key == "new.number").ContentType)
            .IsEqualTo("number");
        await Assert.That(edited.Single(entry => entry.Key == "new.number").Scope)
            .IsEqualTo("all");
        await Assert.That(edited.Single(entry => entry.Key == "new.value").Value)
            .IsEqualTo("a=b=c");
        await Assert.That(edited.Any(entry => entry.Key == "deprecated.key")).IsFalse();

        await Assert.That(source[0].Value).IsEqualTo("true");
        await Assert.That(source.Count).IsEqualTo(3);
    }

    [Test]
    [Arguments("duplicate-set")]
    [Arguments("duplicate-delete")]
    [Arguments("set-delete-conflict")]
    [Arguments("missing-delete")]
    [Arguments("malformed-set")]
    public async Task DirectEdits_RejectAmbiguousOrMistypedOperations(string scenario)
    {
        var source = new[] { Entry("feature.checkout", "true", "boolean", "all") };
        IReadOnlyList<string> sets = scenario switch
        {
            "duplicate-set" => ["feature.checkout=false", "FEATURE.CHECKOUT=true"],
            "set-delete-conflict" => ["feature.checkout=false"],
            "malformed-set" => ["feature.checkout"],
            _ => []
        };
        IReadOnlyList<string> deletes = scenario switch
        {
            "duplicate-delete" => ["feature.checkout", "FEATURE.CHECKOUT"],
            "set-delete-conflict" => ["FEATURE.CHECKOUT"],
            "missing-delete" => ["unknown.key"],
            _ => []
        };

        var exception = CaptureReleaseEditException(() =>
            ReleaseEntryEditing.ApplyDirectEdits(source, sets, deletes));

        await Assert.That(exception).IsNotNull();
    }

    [Test]
    public async Task FileMode_ReadsExactEntryArray()
    {
        using var file = new TestHelpers.TempFile();
        await File.WriteAllTextAsync(
            file.Path,
            """
            [
              {"key":"one","value":"true","contentType":"boolean","scope":"client"},
              {"key":"two","value":"{\"a\":1}","contentType":"json","scope":"server"}
            ]
            """);

        var entries = await ReleaseEntryEditing.ReadFileAsync(
            file.Path,
            CancellationToken.None);

        await Assert.That(entries.Count).IsEqualTo(2);
        await Assert.That(entries[0].Key).IsEqualTo("one");
        await Assert.That(entries[1].Value).IsEqualTo("{\"a\":1}");
        await Assert.That(entries[1].Scope).IsEqualTo("server");
    }

    [Test]
    [Arguments("{}")]
    [Arguments("[null]")]
    [Arguments("[{\"key\":\"one\"}]")]
    public async Task FileMode_RejectsNonArrayNullOrIncompleteEntries(string json)
    {
        using var file = new TestHelpers.TempFile();
        await File.WriteAllTextAsync(file.Path, json);

        ReleaseEditException? exception = null;
        try
        {
            await ReleaseEntryEditing.ReadFileAsync(file.Path, CancellationToken.None);
        }
        catch (ReleaseEditException caught)
        {
            exception = caught;
        }

        await Assert.That(exception).IsNotNull();
    }

    [Test]
    public async Task Editor_UsesVisualAndCleansTemporaryFileAfterSuccess()
    {
        string? temporaryPath = null;
        var editor = new ReleaseEntryEditor(
            name => name == "VISUAL" ? "test-visual" : "test-editor",
            async (command, path, cancellationToken) =>
            {
                await Assert.That(command).IsEqualTo("test-visual");
                await Assert.That(File.Exists(path)).IsTrue();
                temporaryPath = path;
                await File.WriteAllTextAsync(
                    path,
                    """
                    [{"key":"edited","value":"false","contentType":"boolean","scope":"all"}]
                    """,
                    cancellationToken);
                return 0;
            });

        var entries = await editor.EditAsync(
            [Entry("original", "true", "boolean", "all")],
            CancellationToken.None);

        await Assert.That(entries.Single().Key).IsEqualTo("edited");
        await Assert.That(temporaryPath).IsNotNull();
        await Assert.That(File.Exists(temporaryPath!)).IsFalse();
    }

    [Test]
    public async Task Editor_RequiresVisualOrEditorBeforeCreatingTemporaryFile()
    {
        var editor = new ReleaseEntryEditor(_ => null);

        ReleaseEditException? exception = null;
        try
        {
            await editor.EditAsync(
                [Entry("original", "true", "boolean", "all")],
                CancellationToken.None);
        }
        catch (ReleaseEditException caught)
        {
            exception = caught;
        }

        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.Message).Contains("Set VISUAL or EDITOR");
    }

    [Test]
    [Arguments(9, "[]")]
    [Arguments(0, "not-json")]
    public async Task Editor_FailureOrInvalidOutputDoesNotLeakTemporaryFile(
        int exitCode,
        string editedContent)
    {
        string? temporaryPath = null;
        var editor = new ReleaseEntryEditor(
            _ => "test-editor",
            async (_, path, cancellationToken) =>
            {
                temporaryPath = path;
                await File.WriteAllTextAsync(path, editedContent, cancellationToken);
                return exitCode;
            });

        ReleaseEditException? exception = null;
        try
        {
            await editor.EditAsync(
                [Entry("original", "true", "boolean", "all")],
                CancellationToken.None);
        }
        catch (ReleaseEditException caught)
        {
            exception = caught;
        }

        await Assert.That(exception).IsNotNull();
        await Assert.That(temporaryPath).IsNotNull();
        await Assert.That(File.Exists(temporaryPath!)).IsFalse();
    }

    [Test]
    public async Task Editor_CancellationPropagatesAndCleansTemporaryFile()
    {
        string? temporaryPath = null;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var editor = new ReleaseEntryEditor(
            _ => "test-editor",
            (_, path, _) =>
            {
                temporaryPath = path;
                return Task.FromCanceled<int>(cancellation.Token);
            });

        OperationCanceledException? exception = null;
        try
        {
            await editor.EditAsync(
                [Entry("original", "true", "boolean", "all")],
                CancellationToken.None);
        }
        catch (OperationCanceledException caught)
        {
            exception = caught;
        }

        await Assert.That(exception).IsNotNull();
        await Assert.That(temporaryPath).IsNotNull();
        await Assert.That(File.Exists(temporaryPath!)).IsFalse();
    }

    private static ReleaseEditException? CaptureReleaseEditException(Action action)
    {
        try
        {
            action();
            return null;
        }
        catch (ReleaseEditException exception)
        {
            return exception;
        }
    }

    private static ConfigReleaseEntryDto Entry(
        string key,
        string value,
        string contentType,
        string scope)
        => new()
        {
            Key = key,
            Value = value,
            ContentType = contentType,
            Scope = scope
        };
}
