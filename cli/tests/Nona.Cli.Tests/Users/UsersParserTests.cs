using System.CommandLine;
using System.CommandLine.Parsing;

namespace Nona.Cli.Tests.Users;

public sealed class UsersParserTests
{
    [Test]
    [Arguments("admin", false)]
    [Arguments("member", false)]
    [Arguments("viewer", true)]
    [Arguments("editor", true)]
    public async Task Create_ValidatesOrganizationRole(string role, bool hasErrors)
    {
        var result = CreateRoot().Parse(
            ["users", "create", "--name", "Test", "--user-email", "test@example.com", "--role", role]);

        await Assert.That(result.Errors.Count > 0).IsEqualTo(hasErrors);
    }

    private static RootCommand CreateRoot()
    {
        var context = new CliContext(
            CliDefaults.Empty,
            null,
            new CliDefaultsStore(Path.Combine(Path.GetTempPath(), "nona-users-parser-defaults.json")),
            new CliSessionStore(Path.Combine(Path.GetTempPath(), "nona-users-parser-session.json")));
        return Program.CreateRootCommand(context, new Option<bool>("--verbose"));
    }
}
