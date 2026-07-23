using System.CommandLine;
using System.CommandLine.Parsing;

namespace Nona.Cli.Tests.Releases;

public sealed class ReleasesParserTests
{
    [Test]
    [Arguments("list")]
    [Arguments("view")]
    [Arguments("create")]
    [Arguments("amend")]
    [Arguments("activate")]
    [Arguments("clear-active")]
    [Arguments("delete")]
    public async Task ReleasesSubcommand_ResolvesFromPluralCommandGroup(string subcommand)
    {
        var result = CreateRoot().Parse(["releases", subcommand]);

        await Assert.That(result.Errors).IsEmpty();
        await Assert.That(result.CommandResult.Command.Name).IsEqualTo(subcommand);
        await Assert.That(result.CommandResult.Parent?.Symbol.Name).IsEqualTo("releases");
    }

    [Test]
    public async Task SingularReleaseCommandGroup_DoesNotResolve()
    {
        var result = CreateRoot().Parse(["release", "list"]);

        await Assert.That(result.Errors).IsNotEmpty();
    }

    private static RootCommand CreateRoot()
    {
        var context = new CliContext(
            CliDefaults.Empty,
            null,
            new CliDefaultsStore(Path.Combine(Path.GetTempPath(), "nona-parser-defaults.json")),
            new CliSessionStore(Path.Combine(Path.GetTempPath(), "nona-parser-session.json")));
        var verboseOption = new Option<bool>("--verbose");
        return Program.CreateRootCommand(context, verboseOption);
    }
}
