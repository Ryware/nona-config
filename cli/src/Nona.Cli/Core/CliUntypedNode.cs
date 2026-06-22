using System.Globalization;
using Microsoft.Kiota.Abstractions.Serialization;

namespace Nona.Cli;

internal static class CliUntypedNode
{
    public static UntypedNode Integer(int value) => new UntypedInteger(value);

    public static string FormatInteger(UntypedNode? node)
    {
        var value = ToInt32(node);
        return value?.ToString(CultureInfo.InvariantCulture) ?? "?";
    }

    public static int? ToInt32(UntypedNode? node)
    {
        try
        {
            return node switch
            {
                UntypedInteger integer => integer.GetValue(),
                UntypedLong integer => checked((int)integer.GetValue()),
                UntypedString text when int.TryParse(text.GetValue(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
                _ => node?.GetValue() switch
                {
                    int integer => integer,
                    long integer => checked((int)integer),
                    string text when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
                    _ => null
                }
            };
        }
        catch (OverflowException)
        {
            return null;
        }
    }
}
