namespace Nona.Domain;

public static class ConfigEntryPrefix
{
    public const string ValidationError =
        "Prefix may contain only ASCII letters, digits, colons, dots, underscores, and dashes.";

    public static bool IsValid(string? prefix)
    {
        if (string.IsNullOrEmpty(prefix))
        {
            return true;
        }

        foreach (var character in prefix)
        {
            if (character is >= 'A' and <= 'Z'
                or >= 'a' and <= 'z'
                or >= '0' and <= '9'
                or ':' or '.' or '_' or '-')
            {
                continue;
            }

            return false;
        }

        return true;
    }

    public static string? Normalize(string? prefix)
    {
        if (string.IsNullOrEmpty(prefix))
        {
            return null;
        }

        return string.Create(prefix.Length, prefix, static (characters, value) =>
        {
            for (var index = 0; index < value.Length; index++)
            {
                characters[index] = ToUpperAscii(value[index]);
            }
        });
    }

    public static bool StartsWith(string value, string? prefix)
    {
        if (string.IsNullOrEmpty(prefix))
        {
            return true;
        }

        if (prefix.Length > value.Length)
        {
            return false;
        }

        for (var index = 0; index < prefix.Length; index++)
        {
            if (ToUpperAscii(value[index]) != ToUpperAscii(prefix[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static char ToUpperAscii(char character) =>
        character is >= 'a' and <= 'z'
            ? (char)(character - ('a' - 'A'))
            : character;
}
