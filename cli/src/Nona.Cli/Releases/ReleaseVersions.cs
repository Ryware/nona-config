using System.Globalization;

namespace Nona.Cli.Releases;

internal readonly record struct ReleaseVersionLine(int Major, int Minor)
{
    public ReleaseVersion FirstRelease => new(Major, Minor, 0);

    public override string ToString()
        => string.Create(
            CultureInfo.InvariantCulture,
            $"{Major}.{Minor}");
}

internal readonly record struct ReleaseVersion(int Major, int Minor, int Patch)
    : IComparable<ReleaseVersion>
{
    public ReleaseVersionLine Line => new(Major, Minor);

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
    public static bool TryParseLine(string? value, out ReleaseVersionLine version)
    {
        version = default;
        if (!TrySplit(value, expectedParts: 2, out var parts) ||
            !TryParsePart(parts[0], out var major) ||
            !TryParsePart(parts[1], out var minor))
        {
            return false;
        }

        version = new ReleaseVersionLine(major, minor);
        return true;
    }

    public static bool TryParseExact(string? value, out ReleaseVersion version)
    {
        version = default;
        if (!TrySplit(value, expectedParts: 3, out var parts) ||
            !TryParsePart(parts[0], out var major) ||
            !TryParsePart(parts[1], out var minor) ||
            !TryParsePart(parts[2], out var patch))
        {
            return false;
        }

        version = new ReleaseVersion(major, minor, patch);
        return true;
    }

    public static bool TryGetNextPatch(
        ReleaseVersion source,
        IEnumerable<string?> existingVersions,
        out ReleaseVersion next)
    {
        var maxPatch = source.Patch;
        foreach (var value in existingVersions)
        {
            if (TryParseExact(value, out var candidate) &&
                candidate.Line == source.Line &&
                candidate.Patch > maxPatch)
            {
                maxPatch = candidate.Patch;
            }
        }

        if (maxPatch == int.MaxValue)
        {
            next = default;
            return false;
        }

        next = new ReleaseVersion(source.Major, source.Minor, maxPatch + 1);
        return true;
    }

    private static bool TrySplit(
        string? value,
        int expectedParts,
        out string[] parts)
    {
        parts = [];
        if (string.IsNullOrEmpty(value) ||
            value.Length != value.Trim().Length)
        {
            return false;
        }

        parts = value.Split('.');
        return parts.Length == expectedParts;
    }

    private static bool TryParsePart(string value, out int part)
    {
        part = 0;
        if (value.Length == 0 || value.Any(character => character is < '0' or > '9'))
            return false;

        return int.TryParse(
            value,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out part);
    }
}
