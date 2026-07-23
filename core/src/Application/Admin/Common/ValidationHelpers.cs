using System.Text.RegularExpressions;
using Nona.Domain;

namespace Nona.Application.Admin.Common;

public static partial class ValidationHelpers
{
    [GeneratedRegex(@"^[a-zA-Z0-9-]+$")]
    private static partial Regex SlugRegex();

    public static bool IsValidSlug(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) && SlugRegex().IsMatch(value);
    }

    public static bool IsValidKey(string? value)
    {
        return ConfigEntryKey.IsValid(value);
    }
}
