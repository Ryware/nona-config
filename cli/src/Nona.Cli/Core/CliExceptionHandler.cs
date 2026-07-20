using System.CommandLine;
using System.CommandLine.IO;
using System.CommandLine.Invocation;
using Microsoft.Kiota.Abstractions;
using Nona.Migrator.Core.Services;

namespace Nona.Cli;

internal static class CliExceptionHandler
{
    internal static void Handle(
        Exception exception,
        InvocationContext context,
        Option<bool> verboseOption)
    {
        if (exception is OperationCanceledException)
        {
            context.Console.Error.WriteLine("Cancelled.");
            context.ExitCode = CliExitCodes.Cancelled;
            return;
        }

        var error = Describe(exception);
        context.Console.Error.WriteLine(error.Message);

        if (context.ParseResult.GetValueForOption(verboseOption))
        {
            context.Console.Error.WriteLine(exception.ToString());
        }

        context.ExitCode = error.ExitCode;
    }

    internal static CliError Describe(Exception exception)
    {
        if (exception is not ApiException apiException)
        {
            return new CliError(
                $"Error: {TerminalSafeText.SingleLine(exception.Message)}",
                CliExitCodes.UnexpectedError);
        }

        var statusCode = apiException.ResponseStatusCode;
        return new CliError(
            NonaApiExceptionFormatter.Format(apiException),
            ExitCodeFor(statusCode));
    }

    private static int ExitCodeFor(int statusCode)
        => statusCode switch
        {
            400 or 422 => CliExitCodes.ValidationError,
            401 or 403 => CliExitCodes.AuthenticationError,
            404 => CliExitCodes.NotFound,
            409 => CliExitCodes.Conflict,
            >= 500 and <= 599 => CliExitCodes.ServerError,
            >= 400 and <= 499 => CliExitCodes.ValidationError,
            _ => CliExitCodes.UnexpectedError
        };

    internal sealed record CliError(string Message, int ExitCode);
}
