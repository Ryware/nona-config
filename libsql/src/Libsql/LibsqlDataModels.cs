using System.Globalization;
using System.Text;

namespace Nona.Libsql;

public sealed class LibsqlQueryResult
{
    public LibsqlQueryResult(IReadOnlyList<LibsqlRow> rows, int affectedRowCount, long? lastInsertRowId)
    {
        Rows = rows;
        AffectedRowCount = affectedRowCount;
        LastInsertRowId = lastInsertRowId;
    }

    public IReadOnlyList<LibsqlRow> Rows { get; }
    public int AffectedRowCount { get; }
    public long? LastInsertRowId { get; }
}

public sealed class LibsqlRow
{
    private readonly IReadOnlyDictionary<string, object?> _values;

    public LibsqlRow(IReadOnlyList<string> columns, IReadOnlyDictionary<string, object?> values)
    {
        Columns = columns;
        _values = values;
    }

    public IReadOnlyList<string> Columns { get; }

    public object? GetValue(string columnName)
    {
        if (!_values.TryGetValue(columnName, out var value))
        {
            throw new KeyNotFoundException($"Column '{columnName}' was not found in libSQL result.");
        }

        return value;
    }

    public object? GetValue(int index) => GetValue(Columns[index]);

    public string GetString(string columnName) => ConvertToString(GetValue(columnName)) ?? string.Empty;
    public string GetString(int index) => ConvertToString(GetValue(index)) ?? string.Empty;
    public string? GetNullableString(string columnName) => ConvertToString(GetValue(columnName));
    public long GetInt64(string columnName) => ConvertToInt64(GetValue(columnName));
    public long GetInt64(int index) => ConvertToInt64(GetValue(index));
    public int GetInt32(string columnName) => ConvertToInt32(GetValue(columnName));
    public int GetInt32(int index) => ConvertToInt32(GetValue(index));
    public bool GetBoolean(string columnName) => ConvertToBoolean(GetValue(columnName));
    public bool GetBoolean(int index) => ConvertToBoolean(GetValue(index));

    private static string? ConvertToString(object? value)
    {
        return value switch
        {
            null => null,
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture)
        };
    }

    private static long ConvertToInt64(object? value)
    {
        return value switch
        {
            null => 0,
            long longValue => longValue,
            int intValue => intValue,
            bool boolValue => boolValue ? 1L : 0L,
            string stringValue when long.TryParse(stringValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedLong)
                => parsedLong,
            _ => Convert.ToInt64(value, CultureInfo.InvariantCulture)
        };
    }

    private static int ConvertToInt32(object? value)
    {
        return value switch
        {
            null => 0,
            int intValue => intValue,
            long longValue => checked((int)longValue),
            bool boolValue => boolValue ? 1 : 0,
            string stringValue when int.TryParse(stringValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedInt)
                => parsedInt,
            string stringValue when long.TryParse(stringValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedLong)
                => checked((int)parsedLong),
            _ => Convert.ToInt32(value, CultureInfo.InvariantCulture)
        };
    }

    private static bool ConvertToBoolean(object? value)
    {
        return value switch
        {
            null => false,
            bool boolValue => boolValue,
            string stringValue when bool.TryParse(stringValue, out var parsedBool) => parsedBool,
            string stringValue when long.TryParse(stringValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedLong) => parsedLong != 0,
            _ => Convert.ToInt64(value, CultureInfo.InvariantCulture) != 0
        };
    }
}

public sealed record LibsqlStatement(string Sql, object? Parameters = null);

public sealed class LibsqlException : Exception
{
    public LibsqlException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
