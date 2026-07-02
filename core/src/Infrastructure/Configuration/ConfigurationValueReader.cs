using Microsoft.Extensions.Configuration;
using System.Globalization;

namespace Nona.Infrastructure.Configuration;

internal static class ConfigurationValueReader
{
    public static string GetString(IConfiguration configuration, string key, string fallback)
    {
        return configuration[key] ?? fallback;
    }

    public static bool GetBoolean(IConfiguration configuration, string key, bool fallback = false)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        if (bool.TryParse(value, out var parsed))
        {
            return parsed;
        }

        return value is "1" ? true : value is "0" ? false : fallback;
    }

    public static int GetInt32(IConfiguration configuration, string key, int fallback)
    {
        return int.TryParse(configuration[key], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    public static double GetDouble(IConfiguration configuration, string key, double fallback)
    {
        return double.TryParse(configuration[key], NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    public static IReadOnlyList<string> GetStringList(IConfiguration configuration, string key)
    {
        var section = configuration.GetSection(key);
        var values = section.GetChildren()
            .Select(child => child.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToList();

        if (values.Count == 0 && !string.IsNullOrWhiteSpace(section.Value))
        {
            values.Add(section.Value);
        }

        return values;
    }
}
