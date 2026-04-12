using Microsoft.Data.Sqlite;
using System.Globalization;

namespace Nona.Libsql;

internal static class LibsqlSqliteCommandExecutor
{
    public static async Task<LibsqlQueryResult> ExecuteAsync(
        SqliteConnection connection,
        string sql,
        object? parameters = null,
        SqliteTransaction? transaction = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        return await ExecuteStatementAsync(connection, new LibsqlStatement(sql, parameters), transaction, ct);
    }

    public static async Task<IReadOnlyList<LibsqlQueryResult>> ExecuteBatchAsync(
        SqliteConnection connection,
        IEnumerable<LibsqlStatement> statements,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(statements);

        var results = new List<LibsqlQueryResult>();
        foreach (var statement in statements)
        {
            results.Add(await ExecuteStatementAsync(connection, statement, null, ct));
        }

        return results;
    }

    public static void AddParameter(SqliteCommand command, string name, object? value)
    {
        command.Parameters.AddWithValue(NormalizeParameterName(name), NormalizeParameterValue(value) ?? DBNull.Value);
    }

    public static string QuoteIdentifier(string identifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        return $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static async Task<LibsqlQueryResult> ExecuteStatementAsync(
        SqliteConnection connection,
        LibsqlStatement statement,
        SqliteTransaction? transaction,
        CancellationToken ct)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = statement.Sql;

        foreach (var parameter in EnumerateParameters(statement.Parameters))
        {
            AddParameter(command, parameter.Key, parameter.Value);
        }

        if (IsQuery(statement.Sql))
        {
            using var reader = await command.ExecuteReaderAsync(ct);
            var columns = Enumerable.Range(0, reader.FieldCount)
                .Select(reader.GetName)
                .ToList();
            var rows = new List<LibsqlRow>();

            while (await reader.ReadAsync(ct))
            {
                var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                for (var index = 0; index < reader.FieldCount; index++)
                {
                    values[columns[index]] = reader.IsDBNull(index) ? null : reader.GetValue(index);
                }

                rows.Add(new LibsqlRow(columns, values));
            }

            return new LibsqlQueryResult(rows, 0, null);
        }

        var affectedRowCount = await command.ExecuteNonQueryAsync(ct);
        long? lastInsertRowId = null;

        if (IsInsertStatement(statement.Sql))
        {
            using var rowIdCommand = connection.CreateCommand();
            rowIdCommand.Transaction = transaction;
            rowIdCommand.CommandText = "SELECT last_insert_rowid()";
            var value = await rowIdCommand.ExecuteScalarAsync(ct);
            if (value is not null and not DBNull)
            {
                lastInsertRowId = Convert.ToInt64(value, CultureInfo.InvariantCulture);
            }
        }

        return new LibsqlQueryResult([], affectedRowCount, lastInsertRowId);
    }

    private static IEnumerable<KeyValuePair<string, object?>> EnumerateParameters(object? parameters)
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

        foreach (var property in parameters.GetType().GetProperties().Where(property => property.CanRead))
        {
            yield return new KeyValuePair<string, object?>(property.Name, property.GetValue(parameters));
        }
    }

    private static object? NormalizeParameterValue(object? value)
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

    private static string NormalizeParameterName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "@p";
        }

        return name[0] is '@' or ':' or '$' ? $"@{name[1..]}" : $"@{name}";
    }

    private static bool IsQuery(string sql)
    {
        var keyword = GetLeadingKeyword(sql);
        return keyword is "SELECT" or "PRAGMA" or "WITH";
    }

    private static bool IsInsertStatement(string sql)
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
}
