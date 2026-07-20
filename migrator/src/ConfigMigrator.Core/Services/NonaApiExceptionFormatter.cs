using System.Globalization;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Serialization;

namespace Nona.Migrator.Core.Services;

public static class NonaApiExceptionFormatter
{
    public static string Format(ApiException exception)
    {
        var serverMessage = GetStringProperty(exception, "Error")
            ?? GetStringProperty(exception, "Detail")
            ?? GetStringProperty(exception, "Title");
        var errorCode = GetStringProperty(exception, "ErrorCode");

        if (string.IsNullOrWhiteSpace(serverMessage) || IsKiotaFallbackMessage(serverMessage))
        {
            serverMessage = IsKiotaFallbackMessage(exception.Message)
                ? "The server rejected the request"
                : exception.Message;
        }

        var details = exception.ResponseStatusCode > 0
            ? exception.ResponseStatusCode.ToString(CultureInfo.InvariantCulture)
            : null;
        if (!string.IsNullOrWhiteSpace(errorCode))
        {
            details = details is null ? errorCode : $"{details}, {errorCode}";
        }

        var suffix = details is null ? string.Empty : $" ({TerminalSafeText.SingleLine(details)})";
        return $"Error: {TerminalSafeText.SingleLine(serverMessage)}{suffix}";
    }

    private static string? GetStringProperty(ApiException exception, string propertyName)
    {
        if (exception.GetType().GetProperty(propertyName)?.GetValue(exception) is string propertyValue)
            return propertyValue;

        if (exception is not IAdditionalDataHolder additionalDataHolder)
            return null;

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
}

public static class TerminalSafeText
{
    public static string SingleLine(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "An unexpected error occurred";

        var safeCharacters = value.Select(character =>
            char.IsControl(character) ||
            CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.Format
                ? ' '
                : character);

        return string.Join(
            ' ',
            new string(safeCharacters.ToArray())
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
