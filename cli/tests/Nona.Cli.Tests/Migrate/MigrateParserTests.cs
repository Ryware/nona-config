using System.CommandLine;
using System.CommandLine.Parsing;

namespace Nona.Cli.Tests.Migrate;

public sealed class MigrateParserTests
{
    [Test]
    public async Task ParameterStore_AcceptsCompleteCommand()
    {
        var result = CreateRoot().Parse(
        [
            "migrate", "parameter-store",
            "--task-definition", "task-definition.json",
            "--environment", "prod",
            "--region", "eu-central-1",
            "--profile", "production-admin",
            "--dry-run",
            "--base-url", "https://nona.example.com",
            "--project", "cms",
            "--token", "token"
        ]);

        await Assert.That(result.Errors).IsEmpty();
    }

    [Test]
    public async Task ParameterStore_RequiresTaskDefinitionAndEnvironment()
    {
        var result = CreateRoot().Parse(["migrate", "parameter-store"]);

        await Assert.That(result.Errors).IsNotEmpty();
        await Assert.That(result.Errors.Select(error => error.Message).ToArray())
            .Contains(message => message.Contains("--task-definition", StringComparison.Ordinal));
        await Assert.That(result.Errors.Select(error => error.Message).ToArray())
            .Contains(message => message.Contains("--environment", StringComparison.Ordinal));
    }

    private static RootCommand CreateRoot()
    {
        var context = new CliContext(
            CliDefaults.Empty,
            null,
            new CliDefaultsStore(Path.Combine(Path.GetTempPath(), "nona-migrate-parser-defaults.json")),
            new CliSessionStore(Path.Combine(Path.GetTempPath(), "nona-migrate-parser-session.json")));
        return Program.CreateRootCommand(context, new Option<bool>("--verbose"));
    }
}
