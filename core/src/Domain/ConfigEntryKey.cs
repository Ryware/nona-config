namespace Nona.Domain;

public static class ConfigEntryKey
{
    public const string ValidationError =
        "Key must contain an ASCII letter or digit and may only contain ASCII letters, digits, dots, underscores, and dashes.";

    public static bool IsValid(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        var hasAlphaNumeric = false;
        foreach (var character in key)
        {
            if (character is >= 'A' and <= 'Z'
                or >= 'a' and <= 'z'
                or >= '0' and <= '9')
            {
                hasAlphaNumeric = true;
                continue;
            }

            if (character is not ('.' or '_' or '-'))
            {
                return false;
            }
        }

        return hasAlphaNumeric;
    }

    public static void ThrowIfInvalid(string? key, string parameterName)
    {
        if (!IsValid(key))
        {
            throw new ArgumentException(ValidationError, parameterName);
        }
    }
}
