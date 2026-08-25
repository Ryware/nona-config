using System.CommandLine;

namespace Nona.Cli.Tests.Entries;

public class EntriesParserTests
{
    [Test]
    public async Task List_AcceptsPrefixOption()
    {
        var result = CreateRoot().Parse([
            "entries",
            "list",
            "--base-url",
            "https://nona.test",
            "--token",
            "test-token",
            "--project",
            "my-project",
            "--environment",
            "production",
            "--prefix",
            "GroupA:"
        ]);

        await Assert.That(result.Errors).IsEmpty();
    }

    private static RootCommand CreateRoot()
    {
        var context = new CliContext(
            CliDefaults.Empty,
            null,
            new CliDefaultsStore(Path.Combine(Path.GetTempPath(), "nona-entries-parser-defaults.json")),
            new CliSessionStore(Path.Combine(Path.GetTempPath(), "nona-entries-parser-session.json")));
        return Program.CreateRootCommand(context, new Option<bool>("--verbose"));
    }
}
