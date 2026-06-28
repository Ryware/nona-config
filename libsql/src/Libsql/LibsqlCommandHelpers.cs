using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Nona.Libsql;

internal static partial class LibsqlCommandHelpers
{
    public static IEnumerable<KeyValuePair<string, object?>> EnumerateParameters(object? parameters)
    {
        if (parameters is null)
        {
            yield break;
        }

        if (parameters is IReadOnlyDictionary<string, object?> dictionary)
        {
            foreach (var pair in dictionary)
            {
                yield return pair;
            }

            yield break;
        }

        if (parameters is IEnumerable<KeyValuePair<string, object?>> enumerable)
        {
            foreach (var pair in enumerable)
            {
                yield return pair;
            }

            yield break;
        }

        throw new ArgumentException(
            "libSQL parameters must be supplied as a dictionary or key/value pair sequence.",
            nameof(parameters));
    }

    public static string BindParameters(
        DbCommand command,
        string sql,
        object? parameters)
    {
        var values = EnumerateParameters(parameters)
            .ToDictionary(pair => NormalizeParameterKey(pair.Key), pair => pair.Value, StringComparer.OrdinalIgnoreCase);

        if (values.Count == 0)
        {
            return sql;
        }

        var matches = ParameterPattern().Matches(sql);
        if (matches.Count == 0)
        {
            return sql;
        }

        var sb = new StringBuilder(sql.Length + (matches.Count * 2));
        var currentIndex = 0;
        var parameterIndex = 0;

        foreach (Match match in matches)
        {
            sb.Append(sql, currentIndex, match.Index - currentIndex);

            var key = NormalizeParameterKey(match.Value);
            if (!values.TryGetValue(key, out var value))
            {
                throw new KeyNotFoundException($"Parameter '{match.Value}' was not provided for libSQL statement.");
            }

            var parameterName = $"@p_{parameterIndex++}";
            sb.Append(parameterName);

            var dbParameter = command.CreateParameter();
            dbParameter.ParameterName = parameterName;
            dbParameter.Value = NormalizeParameterValue(value);
            command.Parameters.Add(dbParameter);

            currentIndex = match.Index + match.Length;
        }

        sb.Append(sql, currentIndex, sql.Length - currentIndex);
        return sb.ToString();
    }

    public static string InlineParameters(string sql, object? parameters)
    {
        var values = EnumerateParameters(parameters)
            .ToDictionary(pair => NormalizeParameterKey(pair.Key), pair => pair.Value, StringComparer.OrdinalIgnoreCase);

        if (values.Count == 0)
        {
            return sql;
        }

        return ParameterPattern().Replace(sql, match =>
        {
            var key = NormalizeParameterKey(match.Value);
            if (!values.TryGetValue(key, out var value))
            {
                throw new KeyNotFoundException($"Parameter '{match.Value}' was not provided for libSQL statement.");
            }

            return ToSqlLiteral(NormalizeParameterValue(value));
        });
    }

    public static object? NormalizeParameterValue(object? value)
    {
        return value switch
        {
            null or DBNull => null,
            bool boolValue => boolValue ? 1 : 0,
            Enum enumValue => Convert.ToInt64(enumValue, CultureInfo.InvariantCulture),
            DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O", CultureInfo.InvariantCulture),
            _ => value
        };
    }

    public static string NormalizeParameterName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "@p";
        }

        return name[0] is '@' or ':' or '$'
            ? $"@{name[1..]}"
            : $"@{name}";
    }

    private static string NormalizeParameterKey(string name)
    {
        var normalized = NormalizeParameterName(name);
        return normalized[1..];
    }

    private static string ToSqlLiteral(object? value)
    {
        return value switch
        {
            null or DBNull => "NULL",
            byte[] bytes => $"X'{Convert.ToHexString(bytes)}'",
            bool boolValue => boolValue ? "1" : "0",
            sbyte or byte or short or ushort or int or uint or long or ulong => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "0",
            float or double or decimal => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "0",
            _ => $"'{Convert.ToString(value, CultureInfo.InvariantCulture)?.Replace("'", "''")}'"
        };
    }

    public static bool IsQuery(string sql)
    {
        var keyword = GetLeadingKeyword(sql);
        return keyword is "SELECT" or "PRAGMA" or "WITH";
    }

    public static bool ReturnsRows(string sql)
    {
        return IsQuery(sql) || ReturningPattern().IsMatch(sql);
    }

    public static bool IsInsertStatement(string sql)
    {
        var keyword = GetLeadingKeyword(sql);
        return keyword is "INSERT" or "REPLACE";
    }

    private static string GetLeadingKeyword(string sql)
    {
        var trimmed = sql.TrimStart();
        var index = 0;
        while (index < trimmed.Length && char.IsLetter(trimmed[index]))
        {
            index++;
        }

        return index == 0
            ? string.Empty
            : trimmed[..index].ToUpperInvariant();
    }

    [GeneratedRegex(@"[@:$][A-Za-z_][A-Za-z0-9_]*")]
    private static partial Regex ParameterPattern();

    [GeneratedRegex(@"\bRETURNING\b", RegexOptions.IgnoreCase)]
    private static partial Regex ReturningPattern();
}
