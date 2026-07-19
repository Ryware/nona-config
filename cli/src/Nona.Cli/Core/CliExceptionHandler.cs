using System.CommandLine;
using System.CommandLine.IO;
using System.CommandLine.Invocation;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Serialization;

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
                $"Error: {SingleLine(exception.Message)}",
                CliExitCodes.UnexpectedError);
        }

        var statusCode = apiException.ResponseStatusCode;
        var serverMessage = GetStringProperty(apiException, "Error")
            ?? GetStringProperty(apiException, "Detail")
            ?? GetStringProperty(apiException, "Title");
        var errorCode = GetStringProperty(apiException, "ErrorCode");

        if (string.IsNullOrWhiteSpace(serverMessage) || IsKiotaFallbackMessage(serverMessage))
        {
            serverMessage = IsKiotaFallbackMessage(apiException.Message)
                ? "The server rejected the request"
                : apiException.Message;
        }

        var details = statusCode > 0 ? statusCode.ToString() : null;
        if (!string.IsNullOrWhiteSpace(errorCode))
        {
            details = details is null ? errorCode : $"{details}, {errorCode}";
        }

        var suffix = details is null ? string.Empty : $" ({SingleLine(details)})";
        return new CliError(
            $"Error: {SingleLine(serverMessage)}{suffix}",
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

    private static string? GetStringProperty(ApiException exception, string propertyName)
    {
        if (exception.GetType().GetProperty(propertyName)?.GetValue(exception) is string propertyValue)
        {
            return propertyValue;
        }

        if (exception is not IAdditionalDataHolder additionalDataHolder)
        {
            return null;
        }

        var additionalValue = additionalDataHolder.AdditionalData
            .FirstOrDefault(item => item.Key.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
            .Value;

        return additionalValue switch
        {
            string value => value,
            UntypedString value => value.GetValue(),
            _ => null
        };
    }

    private static bool IsKiotaFallbackMessage(string? message)
        => message?.Contains("no error factory is registered", StringComparison.OrdinalIgnoreCase) == true;

    private static string SingleLine(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? "An unexpected error occurred"
            : string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    internal sealed record CliError(string Message, int ExitCode);
}
