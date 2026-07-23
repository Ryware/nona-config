using System.CommandLine;
using System.CommandLine.Parsing;

namespace Nona.Cli.Tests.Releases;

public sealed class ReleasesParserTests
{
    [Test]
    [Arguments("list", null)]
    [Arguments("view", "1.2.0")]
    [Arguments("create", "1.2")]
    [Arguments("amend", "1.2.0")]
    [Arguments("activate", "1.2.0")]
    [Arguments("clear-active", null)]
    [Arguments("delete", "1.2.0")]
    public async Task ReleasesSubcommand_ResolvesFromPluralCommandGroup(
        string subcommand,
        string? version)
    {
        var args = version is null
            ? new[] { "releases", subcommand }
            : new[] { "releases", subcommand, version };
        var result = CreateRoot().Parse(args);

        await Assert.That(result.Errors).IsEmpty();
        await Assert.That(result.CommandResult.Command.Name).IsEqualTo(subcommand);
        await Assert.That(result.CommandResult.Parent?.Symbol.Name).IsEqualTo("releases");
    }

    [Test]
    [Arguments("list", "--json")]
    [Arguments("view", "1.2.0", "--json")]
    [Arguments("create", "1.2", "--activate")]
    [Arguments("amend", "1.2.0", "--set", "feature.checkout=false", "--delete", "old.key")]
    [Arguments("amend", "1.2.0", "--from-file", "./entries.json")]
    [Arguments("activate", "1.2.1")]
    [Arguments("clear-active")]
    [Arguments("delete", "1.2.0")]
    public async Task IntendedPositionalForms_ParseSuccessfully(params string[] commandArgs)
    {
        var result = CreateRoot().Parse(["releases", .. commandArgs]);

        await Assert.That(result.Errors).IsEmpty();
    }

    [Test]
    [Arguments("view", "--version", "1.2.0")]
    [Arguments("create", "--version", "1.2")]
    [Arguments("amend", "--source-version", "1.2.0", "--version", "1.2.1")]
    [Arguments("activate", "--version", "1.2.1")]
    [Arguments("delete", "--version", "1.2.0")]
    public async Task RemovedVersionOptions_AreRejected(params string[] commandArgs)
    {
        var result = CreateRoot().Parse(["releases", .. commandArgs]);

        await Assert.That(result.Errors).IsNotEmpty();
    }

    [Test]
    public async Task Amend_RejectsRemovedEditorOption()
    {
        var result = CreateRoot().Parse(
            ["releases", "amend", "1.2.0", "--editor"]);

        await Assert.That(result.Errors).IsNotEmpty();
    }

    [Test]
    [Arguments("1.2.0")]
    [Arguments("1.2.x")]
    [Arguments("1")]
    [Arguments("1.2 ")]
    public async Task Create_RejectsAnythingExceptMajorMinor(string version)
    {
        var result = CreateRoot().Parse(["releases", "create", version]);

        await Assert.That(result.Errors).IsNotEmpty();
    }

    [Test]
    [Arguments("view", "1.2")]
    [Arguments("view", "1.2.x")]
    [Arguments("amend", "1.2")]
    [Arguments("activate", "1.2.x")]
    [Arguments("delete", "1.2")]
    public async Task ExactManagementCommands_RejectNonExactVersions(
        string subcommand,
        string version)
    {
        var result = CreateRoot().Parse(["releases", subcommand, version]);

        await Assert.That(result.Errors).IsNotEmpty();
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
