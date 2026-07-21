using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using System.CommandLine.Parsing;
using Nona.Cli.Generated.Models;

namespace Nona.Cli.Tests.Core;

public sealed class CliExceptionHandlerTests
{
    [Test]
    [Arguments(400, CliExitCodes.ValidationError)]
    [Arguments(401, CliExitCodes.AuthenticationError)]
    [Arguments(403, CliExitCodes.AuthenticationError)]
    [Arguments(404, CliExitCodes.NotFound)]
    [Arguments(409, CliExitCodes.Conflict)]
    [Arguments(500, CliExitCodes.ServerError)]
    public async Task Describe_MapsApiStatusToDocumentedExitCode(int statusCode, int expectedExitCode)
    {
        var result = CliExceptionHandler.Describe(new ApiProblemDetails
        {
            Detail = "server message",
            ResponseStatusCode = statusCode
        });

        await Assert.That(result.ExitCode).IsEqualTo(expectedExitCode);
        await Assert.That(result.Message).IsEqualTo($"Error: server message ({statusCode})");
    }

    [Test]
    public async Task Parser_PrintsSingleLineServerErrorWithoutStackTrace()
    {
        var exception = new ApiProblemDetails
        {
            Detail = "value is not a valid\nnumber",
            ErrorCode = "INVALID_VALUE",
            ResponseStatusCode = 400
        };

        var (exitCode, output) = await InvokeThrowingCommandAsync(exception);

        await Assert.That(exitCode).IsEqualTo(CliExitCodes.ValidationError);
        await Assert.That(output).IsEqualTo(
            $"Error: value is not a valid number (400, INVALID_VALUE){Environment.NewLine}");
        await Assert.That(output).DoesNotContain(nameof(ApiProblemDetails));
        await Assert.That(output).DoesNotContain(" at ");
    }

    [Test]
    public async Task Parser_VerbosePrintsFullExceptionDetails()
    {
        var exception = new ApiProblemDetails
        {
            Detail = "environment not found",
            ResponseStatusCode = 404
        };

        var (exitCode, output) = await InvokeThrowingCommandAsync(exception, "--verbose");

        await Assert.That(exitCode).IsEqualTo(CliExitCodes.NotFound);
        await Assert.That(output).Contains("Error: environment not found (404)");
        await Assert.That(output).Contains(typeof(ApiProblemDetails).FullName!);
    }

    [Test]
    public async Task Parser_RemovesTerminalControlCharactersFromSummary()
    {
        var exception = new ApiProblemDetails
        {
            Detail = "danger\u001b[31mred\u001b[0m\u0007",
            ErrorCode = "BAD\u001b]8;;https://example.test\u0007CODE",
            ResponseStatusCode = 400
        };

        var (exitCode, output) = await InvokeThrowingCommandAsync(exception);

        await Assert.That(exitCode).IsEqualTo(CliExitCodes.ValidationError);
        await Assert.That(output).DoesNotContain("\u001b");
        await Assert.That(output).DoesNotContain("\u0007");
        await Assert.That(output).Contains("Error: danger [31mred [0m (400, BAD ]8;;https://example.test CODE)");
    }

    private static async Task<(int ExitCode, string Error)> InvokeThrowingCommandAsync(
        Exception exception,
        params string[] args)
    {
        var root = new RootCommand();
        var verboseOption = new Option<bool>("--verbose");
        root.AddGlobalOption(verboseOption);
        root.SetHandler((InvocationContext _) => throw exception);

        var console = new TestConsole();
        var exitCode = await Program.CreateParser(root, verboseOption).InvokeAsync(args, console);
        return (exitCode, console.Error.ToString()!);
    }
}
