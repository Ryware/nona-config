using System.Globalization;

namespace Nona.Cli.Releases;

internal readonly record struct ReleaseVersion(int Major, int Minor, int Patch)
    : IComparable<ReleaseVersion>
{
    public int CompareTo(ReleaseVersion other)
    {
        var majorComparison = Major.CompareTo(other.Major);
        if (majorComparison != 0)
            return majorComparison;

        var minorComparison = Minor.CompareTo(other.Minor);
        return minorComparison != 0
            ? minorComparison
            : Patch.CompareTo(other.Patch);
    }

    public override string ToString()
        => string.Create(
            CultureInfo.InvariantCulture,
            $"{Major}.{Minor}.{Patch}");
}

internal static class ReleaseVersions
{
    public static bool TryParse(string? value, out ReleaseVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var parts = value.Split('.');
        if (parts.Length != 3 ||
            !TryParsePart(parts[0], out var major) ||
            !TryParsePart(parts[1], out var minor) ||
            !TryParsePart(parts[2], out var patch))
        {
            return false;
        }

        version = new ReleaseVersion(major, minor, patch);
        return true;
    }

    private static bool TryParsePart(string value, out int part)
        => int.TryParse(
            value,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out part);
}
