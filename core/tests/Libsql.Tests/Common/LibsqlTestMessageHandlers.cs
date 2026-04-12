using Microsoft.Data.Sqlite;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Nona.Libsql.Tests.Common;

internal sealed class ThrowingHttpMessageHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        throw new HttpRequestException("Simulated network failure.");
    }
}

internal sealed class FakeLibsqlMessageHandler : HttpMessageHandler
{
    private readonly SqliteConnection _connection;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public FakeLibsqlMessageHandler()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!string.Equals(request.RequestUri?.AbsolutePath, "/v2/pipeline", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent("{\"error\":\"Not found\"}", Encoding.UTF8, "application/json")
                };
            }

            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(body);

            var results = new List<object?>();
            foreach (var requestElement in document.RootElement.GetProperty("requests").EnumerateArray())
            {
                var type = requestElement.GetProperty("type").GetString();
                if (string.Equals(type, "close", StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(new { type = "ok", response = new { type = "close" } });
                    continue;
                }

                if (!string.Equals(type, "execute", StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(new { type = "error", error = new { message = $"Unsupported request type '{type}'." } });
                    continue;
                }

                results.Add(await ExecuteStatementAsync(requestElement.GetProperty("stmt"), cancellationToken));
            }

            var responseJson = JsonSerializer.Serialize(new
            {
                baton = (string?)null,
                base_url = (string?)null,
                results
            });

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };
        }
        finally
        {
            _gate.Release();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _gate.Dispose();
            _connection.Dispose();
        }

        base.Dispose(disposing);
    }

    private async Task<object> ExecuteStatementAsync(JsonElement statementElement, CancellationToken cancellationToken)
    {
        try
        {
            using var command = _connection.CreateCommand();
            command.CommandText = statementElement.GetProperty("sql").GetString() ?? string.Empty;

            if (statementElement.TryGetProperty("named_args", out var namedArgsElement) &&
                namedArgsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var argElement in namedArgsElement.EnumerateArray())
                {
                    var name = argElement.GetProperty("name").GetString() ?? string.Empty;
                    var value = ConvertArgument(argElement.GetProperty("value"));
                    command.Parameters.AddWithValue(NormalizeParameterName(name), value ?? DBNull.Value);
                }
            }

            if (IsQuery(command.CommandText))
            {
                using var reader = await command.ExecuteReaderAsync(cancellationToken);
                var columns = Enumerable.Range(0, reader.FieldCount)
                    .Select(index => new { name = reader.GetName(index) })
                    .ToList();
                var rows = new List<object>();

                while (await reader.ReadAsync(cancellationToken))
                {
                    var row = new List<object?>();
                    for (var index = 0; index < reader.FieldCount; index++)
                    {
                        row.Add(CreateResultValue(reader.IsDBNull(index) ? null : reader.GetValue(index)));
                    }

                    rows.Add(row);
                }

                var lastInsertRowId = await GetLastInsertRowIdAsync(cancellationToken);

                return new
                {
                    type = "ok",
                    response = new
                    {
                        type = "execute",
                        result = new
                        {
                            cols = columns,
                            rows,
                            affected_row_count = 0,
                            last_insert_rowid = lastInsertRowId == 0 ? null : lastInsertRowId.ToString(CultureInfo.InvariantCulture),
                            replication_index = "1"
                        }
                    }
                };
            }

            var affectedRowCount = await command.ExecuteNonQueryAsync(cancellationToken);
            var lastInsertAfterWrite = await GetLastInsertRowIdAsync(cancellationToken);
            return new
            {
                type = "ok",
                response = new
                {
                    type = "execute",
                    result = new
                    {
                        cols = Array.Empty<object>(),
                        rows = Array.Empty<object>(),
                        affected_row_count = affectedRowCount,
                        last_insert_rowid = lastInsertAfterWrite == 0 ? null : lastInsertAfterWrite.ToString(CultureInfo.InvariantCulture),
                        replication_index = "1"
                    }
                }
            };
        }
        catch (Exception ex)
        {
            return new
            {
                type = "error",
                error = new
                {
                    message = ex.Message
                }
            };
        }
    }

    private static string NormalizeParameterName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "@p";
        }

        return name[0] is '@' or ':' or '$' ? $"@{name[1..]}" : $"@{name}";
    }

    private static object? ConvertArgument(JsonElement valueElement)
    {
        var type = valueElement.GetProperty("type").GetString();

        return type switch
        {
            "null" => null,
            "integer" => ParseInteger(valueElement),
            "float" => ParseFloat(valueElement),
            "text" => valueElement.GetProperty("value").GetString(),
            "blob" => Convert.FromBase64String(valueElement.GetProperty("base64").GetString() ?? string.Empty),
            _ => valueElement.TryGetProperty("value", out var nestedValue)
                ? nestedValue.ToString()
                : null
        };
    }

    private static long ParseInteger(JsonElement valueElement)
    {
        var rawValue = valueElement.GetProperty("value");
        return rawValue.ValueKind switch
        {
            JsonValueKind.Number => rawValue.GetInt64(),
            JsonValueKind.String => long.Parse(rawValue.GetString() ?? "0", CultureInfo.InvariantCulture),
            _ => 0
        };
    }

    private static double ParseFloat(JsonElement valueElement)
    {
        var rawValue = valueElement.GetProperty("value");
        return rawValue.ValueKind switch
        {
            JsonValueKind.Number => rawValue.GetDouble(),
            JsonValueKind.String => double.Parse(rawValue.GetString() ?? "0", CultureInfo.InvariantCulture),
            _ => 0
        };
    }

    private static bool IsQuery(string sql)
    {
        var trimmed = sql.TrimStart();
        return trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("WITH", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("PRAGMA", StringComparison.OrdinalIgnoreCase);
    }

    private static object CreateResultValue(object? value)
    {
        if (value is null)
        {
            return new { type = "null" };
        }

        if (value is byte[] bytes)
        {
            return new { type = "blob", base64 = Convert.ToBase64String(bytes) };
        }

        if (value is sbyte or byte or short or ushort or int or uint or long or ulong or bool)
        {
            var numericValue = value is bool boolValue
                ? (boolValue ? 1L : 0L)
                : Convert.ToInt64(value, CultureInfo.InvariantCulture);

            return new
            {
                type = "integer",
                value = numericValue.ToString(CultureInfo.InvariantCulture)
            };
        }

        if (value is float or double or decimal)
        {
            return new
            {
                type = "float",
                value = Convert.ToString(value, CultureInfo.InvariantCulture)
            };
        }

        return new
        {
            type = "text",
            value = Convert.ToString(value, CultureInfo.InvariantCulture)
        };
    }

    private async Task<long> GetLastInsertRowIdAsync(CancellationToken cancellationToken)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT last_insert_rowid()";
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null or DBNull ? 0 : Convert.ToInt64(value, CultureInfo.InvariantCulture);
    }
}
