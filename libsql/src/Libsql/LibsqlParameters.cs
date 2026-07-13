namespace Nona.Libsql;

public static class LibsqlParameters
{
    public static IReadOnlyDictionary<string, object?> Create(params (string Name, object? Value)[] parameters)
    {
        var values = new Dictionary<string, object?>(parameters.Length, StringComparer.OrdinalIgnoreCase);
        foreach (var (name, value) in parameters)
        {
            values[name] = value;
        }

        return values;
    }
}
