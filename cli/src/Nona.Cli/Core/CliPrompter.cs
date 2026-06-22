namespace Nona.Cli;

internal static class CliPrompter
{
    /// <summary>
    /// Returns <paramref name="provided"/> if non-empty, otherwise prompts the user.
    /// Keeps re-prompting until a non-empty value is entered.
    /// </summary>
    public static string Required(string? provided, string label)
    {
        if (!string.IsNullOrWhiteSpace(provided))
            return provided;

        while (true)
        {
            Console.Write($"{label}: ");
            var input = Console.ReadLine()?.Trim();
            if (!string.IsNullOrWhiteSpace(input))
                return input;

            Console.Error.WriteLine($"  {label} cannot be empty.");
        }
    }

    /// <summary>
    /// Returns <paramref name="provided"/> if set, otherwise prompts.
    /// Pressing Enter without input returns null (skip).
    /// </summary>
    public static string? Optional(string? provided, string label)
    {
        if (provided is not null)
            return provided;

        Console.Write($"{label} (optional): ");
        var input = Console.ReadLine()?.Trim();
        return string.IsNullOrWhiteSpace(input) ? null : input;
    }

    public static int RequiredInt(int? provided, string label)
    {
        if (provided is not null)
            return provided.Value;

        while (true)
        {
            var value = Required(null, label);
            if (int.TryParse(value, out var parsed))
                return parsed;

            Console.Error.WriteLine($"  {label} must be a whole number.");
        }
    }
}
