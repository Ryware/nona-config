using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nona.Libsql;

public sealed class LibsqlHttpDatabaseClient : ILibsqlDatabaseClient
{
    private readonly HttpClient _httpClient;

    public LibsqlHttpDatabaseClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<LibsqlQueryResult> ExecuteAsync(string sql, object? parameters = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        var results = await ExecuteBatchAsync([new LibsqlStatement(sql, parameters)], ct);
        return results[0];
    }

    public async Task<IReadOnlyList<LibsqlQueryResult>> ExecuteBatchAsync(
        IEnumerable<LibsqlStatement> statements,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(statements);

        var requests = statements
            .Select(statement => new LibsqlPipelineRequest
            {
                Type = "execute",
                Stmt = new LibsqlStatementPayload
                {
                    Sql = statement.Sql,
                    NamedArgs = BuildNamedArgs(statement.Parameters)
                }
            })
            .ToList();

        requests.Add(new LibsqlPipelineRequest { Type = "close" });

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsync("v2/pipeline", JsonContent.Create(new LibsqlPipelinePayload
            {
                Requests = requests
            }), ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new LibsqlException("libSQL HTTP request failed.", null, ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                throw new LibsqlException(
                    $"libSQL HTTP request failed with status {(int)response.StatusCode}: {body}",
                    response.StatusCode);
            }

            try
            {
                await using var stream = await response.Content.ReadAsStreamAsync(ct);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
                return ParsePipelineResults(document.RootElement);
            }
            catch (LibsqlException)
            {
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
            {
                throw new LibsqlException("libSQL response processing failed.", null, ex);
            }
        }
    }

    public static string NormalizeBaseUrl(string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        var trimmed = url.Trim().TrimEnd('/');

        if (trimmed.StartsWith("libsql://", StringComparison.OrdinalIgnoreCase))
        {
            return $"https://{trimmed["libsql://".Length..]}";
        }

        if (trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        throw new InvalidOperationException("libSQL URL must start with libsql://, https://, or http://.");
    }

    private static IReadOnlyList<LibsqlQueryResult> ParsePipelineResults(JsonElement root)
    {
        if (!root.TryGetProperty("results", out var resultsElement) || resultsElement.ValueKind != JsonValueKind.Array)
        {
            throw new LibsqlException("libSQL response did not contain a valid results array.");
        }

        var results = new List<LibsqlQueryResult>();

        foreach (var step in resultsElement.EnumerateArray())
        {
            var stepType = step.TryGetProperty("type", out var typeElement)
                ? typeElement.GetString()
                : null;

            if (!string.Equals(stepType, "ok", StringComparison.OrdinalIgnoreCase))
            {
                throw CreatePipelineException(step);
            }

            if (!step.TryGetProperty("response", out var responseElement))
            {
                continue;
            }

            var responseType = responseElement.TryGetProperty("type", out var responseTypeElement)
                ? responseTypeElement.GetString()
                : null;

            if (!string.Equals(responseType, "execute", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!responseElement.TryGetProperty("result", out var resultElement))
            {
                throw new LibsqlException("libSQL execute response did not contain a result payload.");
            }

            results.Add(ParseExecuteResult(resultElement));
        }

        return results;
    }

    private static LibsqlException CreatePipelineException(JsonElement step)
    {
        if (step.TryGetProperty("error", out var errorElement))
        {
            return new LibsqlException(ExtractErrorMessage(errorElement));
        }

        return new LibsqlException($"libSQL request failed: {step.GetRawText()}");
    }

    private static string ExtractErrorMessage(JsonElement errorElement)
    {
        if (errorElement.ValueKind == JsonValueKind.String)
        {
            return errorElement.GetString() ?? "libSQL request failed.";
        }

        if (errorElement.ValueKind == JsonValueKind.Object)
        {
            if (errorElement.TryGetProperty("message", out var messageElement))
            {
                return messageElement.GetString() ?? "libSQL request failed.";
            }

            if (errorElement.TryGetProperty("error", out var nestedErrorElement))
            {
                return ExtractErrorMessage(nestedErrorElement);
            }
        }

        return $"libSQL request failed: {errorElement.GetRawText()}";
    }

    private static LibsqlQueryResult ParseExecuteResult(JsonElement resultElement)
    {
        var columns = resultElement.TryGetProperty("cols", out var columnsElement)
            ? ParseColumns(columnsElement)
            : [];

        var rows = resultElement.TryGetProperty("rows", out var rowsElement)
            ? ParseRows(columns, rowsElement)
            : [];

        var affectedRowCount = resultElement.TryGetProperty("affected_row_count", out var affectedRowCountElement)
            ? ConvertToInt32(ParseScalarValue(affectedRowCountElement))
            : 0;

        var lastInsertRowId = resultElement.TryGetProperty("last_insert_rowid", out var lastInsertRowIdElement)
            ? ConvertToNullableInt64(ParseScalarValue(lastInsertRowIdElement))
            : null;

        return new LibsqlQueryResult(rows, affectedRowCount, lastInsertRowId);
    }

    private static IReadOnlyList<string> ParseColumns(JsonElement columnsElement)
    {
        if (columnsElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var columns = new List<string>();
        foreach (var columnElement in columnsElement.EnumerateArray())
        {
            if (columnElement.ValueKind == JsonValueKind.String)
            {
                columns.Add(columnElement.GetString() ?? string.Empty);
                continue;
            }

            if (columnElement.ValueKind == JsonValueKind.Object &&
                columnElement.TryGetProperty("name", out var nameElement))
            {
                columns.Add(nameElement.GetString() ?? string.Empty);
                continue;
            }

            columns.Add(string.Empty);
        }

        return columns;
    }

    private static IReadOnlyList<LibsqlRow> ParseRows(IReadOnlyList<string> columns, JsonElement rowsElement)
    {
        if (rowsElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var rows = new List<LibsqlRow>();

        foreach (var rowElement in rowsElement.EnumerateArray())
        {
            if (rowElement.ValueKind == JsonValueKind.Array)
            {
                rows.Add(ParseArrayRow(columns, rowElement));
                continue;
            }

            if (rowElement.ValueKind == JsonValueKind.Object &&
                rowElement.TryGetProperty("values", out var valuesElement) &&
                valuesElement.ValueKind == JsonValueKind.Array)
            {
                rows.Add(ParseArrayRow(columns, valuesElement));
                continue;
            }

            if (rowElement.ValueKind == JsonValueKind.Object)
            {
                var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                var orderedColumns = new List<string>();

                foreach (var property in rowElement.EnumerateObject())
                {
                    orderedColumns.Add(property.Name);
                    values[property.Name] = ParseScalarValue(property.Value);
                }

                rows.Add(new LibsqlRow(orderedColumns, values));
            }
        }

        return rows;
    }

    private static LibsqlRow ParseArrayRow(IReadOnlyList<string> columns, JsonElement rowElement)
    {
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var orderedColumns = new List<string>();
        var index = 0;

        foreach (var valueElement in rowElement.EnumerateArray())
        {
            var columnName = index < columns.Count && !string.IsNullOrWhiteSpace(columns[index])
                ? columns[index]
                : $"col{index}";

            orderedColumns.Add(columnName);
            values[columnName] = ParseScalarValue(valueElement);
            index++;
        }

        return new LibsqlRow(orderedColumns, values);
    }

    private static object? ParseScalarValue(JsonElement valueElement)
    {
        return valueElement.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => valueElement.GetString(),
            JsonValueKind.Number => valueElement.TryGetInt64(out var longValue)
                ? longValue
                : valueElement.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Object => ParseTypedValue(valueElement),
            _ => valueElement.GetRawText()
        };
    }

    private static object? ParseTypedValue(JsonElement valueElement)
    {
        if (!valueElement.TryGetProperty("type", out var typeElement))
        {
            if (valueElement.TryGetProperty("value", out var nestedValueElement))
            {
                return ParseScalarValue(nestedValueElement);
            }

            return valueElement.GetRawText();
        }

        var type = typeElement.GetString();

        return type switch
        {
            "null" => null,
            "integer" => ParseIntegerValue(valueElement),
            "float" => ParseFloatValue(valueElement),
            "text" => valueElement.TryGetProperty("value", out var textValueElement)
                ? textValueElement.GetString()
                : string.Empty,
            "blob" => valueElement.TryGetProperty("base64", out var blobValueElement)
                ? Convert.FromBase64String(blobValueElement.GetString() ?? string.Empty)
                : null,
            _ => valueElement.TryGetProperty("value", out var rawValueElement)
                ? ParseScalarValue(rawValueElement)
                : valueElement.GetRawText()
        };
    }

    private static long ParseIntegerValue(JsonElement valueElement)
    {
        if (valueElement.TryGetProperty("value", out var nestedValueElement))
        {
            if (nestedValueElement.ValueKind == JsonValueKind.Number && nestedValueElement.TryGetInt64(out var numericValue))
            {
                return numericValue;
            }

            if (nestedValueElement.ValueKind == JsonValueKind.String &&
                long.TryParse(nestedValueElement.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var stringValue))
            {
                return stringValue;
            }
        }

        return 0;
    }

    private static double ParseFloatValue(JsonElement valueElement)
    {
        if (valueElement.TryGetProperty("value", out var nestedValueElement))
        {
            if (nestedValueElement.ValueKind == JsonValueKind.Number)
            {
                return nestedValueElement.GetDouble();
            }

            if (nestedValueElement.ValueKind == JsonValueKind.String &&
                double.TryParse(nestedValueElement.GetString(), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var stringValue))
            {
                return stringValue;
            }
        }

        return 0;
    }

    private static List<LibsqlNamedArgument>? BuildNamedArgs(object? parameters)
    {
        if (parameters is null)
        {
            return null;
        }

        IEnumerable<KeyValuePair<string, object?>> pairs = parameters switch
        {
            IReadOnlyDictionary<string, object?> dictionary => dictionary,
            IEnumerable<KeyValuePair<string, object?>> enumerable => enumerable,
            _ => parameters.GetType()
                .GetProperties()
                .Where(property => property.CanRead)
                .Select(property => new KeyValuePair<string, object?>(property.Name, property.GetValue(parameters)))
        };

        return pairs
            .Select(pair => new LibsqlNamedArgument
            {
                Name = pair.Key.TrimStart('@', ':', '$'),
                Value = CreateValue(pair.Value)
            })
            .ToList();
    }

    private static LibsqlValue CreateValue(object? value)
    {
        if (value is null or DBNull)
        {
            return new LibsqlValue { Type = "null" };
        }

        if (value is bool boolValue)
        {
            return new LibsqlValue { Type = "integer", Value = boolValue ? "1" : "0" };
        }

        if (value is Enum enumValue)
        {
            return new LibsqlValue
            {
                Type = "integer",
                Value = Convert.ToInt64(enumValue, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture)
            };
        }

        if (value is byte[] bytes)
        {
            return new LibsqlValue { Type = "blob", Base64 = Convert.ToBase64String(bytes) };
        }

        if (value is sbyte or byte or short or ushort or int or uint or long or ulong)
        {
            return new LibsqlValue
            {
                Type = "integer",
                Value = Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture)
            };
        }

        if (value is float or double or decimal)
        {
            return new LibsqlValue
            {
                Type = "float",
                Value = Convert.ToString(value, CultureInfo.InvariantCulture)
            };
        }

        if (value is DateTime dateTime)
        {
            return new LibsqlValue { Type = "text", Value = dateTime.ToString("O", CultureInfo.InvariantCulture) };
        }

        if (value is DateTimeOffset dateTimeOffset)
        {
            return new LibsqlValue { Type = "text", Value = dateTimeOffset.ToString("O", CultureInfo.InvariantCulture) };
        }

        return new LibsqlValue
        {
            Type = "text",
            Value = Convert.ToString(value, CultureInfo.InvariantCulture)
        };
    }

    private static int ConvertToInt32(object? value)
    {
        if (value is null)
        {
            return 0;
        }

        return value switch
        {
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

    private static long? ConvertToNullableInt64(object? value)
    {
        if (value is null)
        {
            return null;
        }

        return value switch
        {
            long longValue => longValue,
            int intValue => intValue,
            bool boolValue => boolValue ? 1L : 0L,
            string stringValue when long.TryParse(stringValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedLong)
                => parsedLong,
            _ => Convert.ToInt64(value, CultureInfo.InvariantCulture)
        };
    }

    private sealed class LibsqlPipelinePayload
    {
        [JsonPropertyName("requests")]
        public required List<LibsqlPipelineRequest> Requests { get; init; }
    }

    private sealed class LibsqlPipelineRequest
    {
        [JsonPropertyName("type")]
        public required string Type { get; init; }

        [JsonPropertyName("stmt")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public LibsqlStatementPayload? Stmt { get; init; }
    }

    private sealed class LibsqlStatementPayload
    {
        [JsonPropertyName("sql")]
        public required string Sql { get; init; }

        [JsonPropertyName("named_args")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<LibsqlNamedArgument>? NamedArgs { get; init; }
    }

    private sealed class LibsqlNamedArgument
    {
        [JsonPropertyName("name")]
        public required string Name { get; init; }

        [JsonPropertyName("value")]
        public required LibsqlValue Value { get; init; }
    }

    private sealed class LibsqlValue
    {
        [JsonPropertyName("type")]
        public required string Type { get; init; }

        [JsonPropertyName("value")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Value { get; init; }

        [JsonPropertyName("base64")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Base64 { get; init; }
    }
}

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
    public LibsqlException(string message, System.Net.HttpStatusCode? statusCode = null, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }

    public System.Net.HttpStatusCode? StatusCode { get; }
}
