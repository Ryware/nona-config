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

        var validationErrors = GetValidationErrors(exception)
            .OrderBy(error => error.Field, StringComparer.Ordinal)
            .Select(error =>
                $"{TerminalSafeText.SingleLine(error.Field)}: {TerminalSafeText.SingleLine(error.Message)}")
            .ToArray();
        var validationSuffix = validationErrors.Length == 0
            ? string.Empty
            : $" {string.Join("; ", validationErrors)}";
        var suffix = details is null ? string.Empty : $" ({TerminalSafeText.SingleLine(details)})";
        return $"Error: {TerminalSafeText.SingleLine(serverMessage)}{validationSuffix}{suffix}";
    }

    private static string? GetStringProperty(ApiException exception, string propertyName)
    {
        if (exception.GetType().GetProperty(propertyName)?.GetValue(exception) is string propertyValue)
            return propertyValue;

        var additionalValue = GetAdditionalDataValue(exception, propertyName);

        return additionalValue switch
        {
            string value => value,
            UntypedString value => value.GetValue(),
            _ => null
        };
    }

    private static IEnumerable<ValidationError> GetValidationErrors(ApiException exception)
    {
        var errors = exception.GetType().GetProperty("Errors")?.GetValue(exception)
            ?? GetAdditionalDataValue(exception, "Errors");

        foreach (var field in GetErrorFields(errors))
        {
            foreach (var message in GetErrorMessages(field.Value))
            {
                yield return new ValidationError(field.Key, message);
            }
        }
    }

    private static IEnumerable<KeyValuePair<string, object?>> GetErrorFields(object? errors)
    {
        if (errors is IAdditionalDataHolder additionalDataHolder)
        {
            foreach (var field in additionalDataHolder.AdditionalData)
                yield return new KeyValuePair<string, object?>(field.Key, field.Value);

            yield break;
        }

        if (errors is UntypedObject untypedObject)
        {
            foreach (var field in untypedObject.GetValue())
                yield return new KeyValuePair<string, object?>(field.Key, field.Value);

            yield break;
        }

        if (errors is IReadOnlyDictionary<string, string[]> dictionary)
        {
            foreach (var field in dictionary)
                yield return new KeyValuePair<string, object?>(field.Key, field.Value);
        }
    }

    private static IEnumerable<string> GetErrorMessages(object? value)
    {
        if (value is string message)
        {
            yield return message;
            yield break;
        }

        if (value is UntypedString untypedString)
        {
            if (untypedString.GetValue() is { } untypedMessage)
                yield return untypedMessage;

            yield break;
        }

        if (value is UntypedArray untypedArray)
        {
            foreach (var item in untypedArray.GetValue())
            {
                foreach (var nestedMessage in GetErrorMessages(item))
                    yield return nestedMessage;
            }

            yield break;
        }

        if (value is IEnumerable<string> messages)
        {
            foreach (var item in messages)
                yield return item;
        }
    }

    private static object? GetAdditionalDataValue(object value, string propertyName)
    {
        if (value is not IAdditionalDataHolder additionalDataHolder)
            return null;

        return additionalDataHolder.AdditionalData
            .FirstOrDefault(item => item.Key.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
            .Value;
    }

    private static bool IsKiotaFallbackMessage(string? message)
        => message?.Contains("no error factory is registered", StringComparison.OrdinalIgnoreCase) == true;

    private sealed record ValidationError(string Field, string Message);
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
