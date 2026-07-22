using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using System.CommandLine.Parsing;
using Microsoft.Kiota.Abstractions.Serialization;
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

    [Test]
    public async Task Describe_FlattensAndSanitizesEveryValidationError()
    {
        var exception = new ApiValidationProblemDetails
        {
            Detail = "One or more validation errors occurred.",
            ResponseStatusCode = 400,
            Errors = new ApiValidationProblemDetails_errors
            {
                AdditionalData = new Dictionary<string, object>
                {
                    ["Password"] = new UntypedArray([
                        new UntypedString("Password is too short.")
                    ]),
                    ["Email\u001b[31m"] = new UntypedArray([
                        new UntypedString("Email must be valid.\nUse a full address."),
                        new UntypedString("Email must not contain\u0007 control characters.")
                    ])
                }
            }
        };

        var result = CliExceptionHandler.Describe(exception);

        await Assert.That(result.ExitCode).IsEqualTo(CliExitCodes.ValidationError);
        await Assert.That(result.Message).IsEqualTo(
            "Error: One or more validation errors occurred. " +
            "Email [31m: Email must be valid. Use a full address.; " +
            "Email [31m: Email must not contain control characters.; " +
            "Password: Password is too short. (400)");
        await Assert.That(result.Message).DoesNotContain("\u001b");
        await Assert.That(result.Message).DoesNotContain("\u0007");
        await Assert.That(result.Message).DoesNotContain("\n");
    }

    [Test]
    public async Task Describe_DetectsValidationErrorsInAdditionalData()
    {
        var exception = new ApiProblemDetails
        {
            Detail = "One or more validation errors occurred.",
            ResponseStatusCode = 422,
            AdditionalData = new Dictionary<string, object>
            {
                ["errors"] = new UntypedObject(new Dictionary<string, UntypedNode>
                {
                    ["Name"] = new UntypedArray([
                        new UntypedString("Name is required.")
                    ])
                })
            }
        };

        var result = CliExceptionHandler.Describe(exception);

        await Assert.That(result.ExitCode).IsEqualTo(CliExitCodes.ValidationError);
        await Assert.That(result.Message).IsEqualTo(
            "Error: One or more validation errors occurred. Name: Name is required. (422)");
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
