namespace Nona.Application.Admin.ConfigReleases;

internal enum ConfigReleaseVersionKind
{
    Exact,
    Line
}

internal sealed record ConfigReleaseVersionSelector(
    ConfigReleaseVersionKind Kind,
    int Major,
    int Minor,
    int? Patch,
    string Normalized);

internal static class ConfigReleaseVersions
{
    public static bool TryParseExact(string? version, out ConfigReleaseVersionSelector selector)
    {
        selector = default!;
        if (!TryParse(version, allowLine: false, out var parsed))
        {
            return false;
        }

        selector = parsed;
        return true;
    }

    public static bool TryParseSelector(string? version, out ConfigReleaseVersionSelector selector)
    {
        return TryParse(version, allowLine: true, out selector);
    }

    private static bool TryParse(string? version, bool allowLine, out ConfigReleaseVersionSelector selector)
    {
        selector = default!;
        if (string.IsNullOrWhiteSpace(version))
        {
            return false;
        }

        var parts = version.Trim().Split('.');
        if (parts.Length != 3)
        {
            return false;
        }

        if (!TryParseNumber(parts[0], out var major)
            || !TryParseNumber(parts[1], out var minor))
        {
            return false;
        }

        if (allowLine && parts[2].Equals("x", StringComparison.OrdinalIgnoreCase))
        {
            selector = new ConfigReleaseVersionSelector(
                ConfigReleaseVersionKind.Line,
                major,
                minor,
                null,
                $"{major}.{minor}.x");
            return true;
        }

        if (!TryParseNumber(parts[2], out var patch))
        {
            return false;
        }

        selector = new ConfigReleaseVersionSelector(
            ConfigReleaseVersionKind.Exact,
            major,
            minor,
            patch,
            $"{major}.{minor}.{patch}");
        return true;
    }

    private static bool TryParseNumber(string value, out int number)
    {
        number = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        foreach (var character in value)
        {
            if (character < '0' || character > '9')
            {
                return false;
            }
        }

        return int.TryParse(value, out number);
    }
}
