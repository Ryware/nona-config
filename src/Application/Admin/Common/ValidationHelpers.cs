using System.Text.RegularExpressions;

namespace Nona.Application.Admin.Common;

public static partial class ValidationHelpers
{
    [GeneratedRegex(@"^[a-zA-Z0-9-]+$")]
    private static partial Regex SlugRegex();

    [GeneratedRegex(@"^\S+$")]
    private static partial Regex KeyRegex();

    public static bool IsValidSlug(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) && SlugRegex().IsMatch(value);
    }

    public static bool IsValidKey(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) && KeyRegex().IsMatch(value);
    }
}
