namespace Nona.Application.Common;

public static class ConfigEntryMetadata
{
    public const int MaxDescriptionLength = 500;
    public const int MaxUnitLength = 32;

    public static string? NormalizeDescription(string? description)
        => description?.Trim();

    public static string? NormalizeUnit(string? unit)
    {
        if (string.IsNullOrWhiteSpace(unit))
            return null;

        return unit.Trim();
    }
}
