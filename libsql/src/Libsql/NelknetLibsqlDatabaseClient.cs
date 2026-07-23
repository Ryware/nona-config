using Microsoft.Extensions.Options;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nona.Libsql;

public sealed partial class NelknetLibsqlDatabaseClient : ILibsqlDatabaseClient, IDisposable
{
    private readonly HttpClient _httpClient;
    private bool _disposed;

    public NelknetLibsqlDatabaseClient(IOptions<LibsqlOptions> options)
        : this(options.Value)
    {
    }

    public NelknetLibsqlDatabaseClient(string connectionString, int commandTimeoutSeconds = 30)
        : this(new LibsqlOptions
        {
            DataSource = connectionString,
            TimeoutSeconds = commandTimeoutSeconds
        })
    {
    }

    private NelknetLibsqlDatabaseClient(LibsqlOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.DataSource);

        if (options.EnableLocalReplica)
        {
            throw new NotSupportedException(
                "Storage:Libsql:EnableLocalReplica is not supported by the AOT libSQL client. " +
                "Use a managed sqld replica with --primary-grpc-url instead.");
        }

        if (!IsHttpDataSource(options.DataSource))
        {
            throw new NotSupportedException(
                "Nona requires a sqld/libSQL HTTP data source. Configure Storage:Libsql:ManagedPrimary:Enabled=true " +
                "or set ConnectionStrings:Libsql / Storage:Libsql:DataSource to http(s):// or libsql://.");
        }

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(NormalizeHttpDataSource(options.DataSource)),
            Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds)
        };

        if (!string.IsNullOrWhiteSpace(options.AuthToken))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", options.AuthToken);
        }
    }

    public async Task<LibsqlQueryResult> ExecuteAsync(string sql, object? parameters = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        var statement = new LibsqlStatement(sql, parameters);
        var results = await ExecuteHttpStatementsAsync([statement], ct);
        return results[0];
    }

    public async Task<IReadOnlyList<LibsqlQueryResult>> ExecuteBatchAsync(
        IEnumerable<LibsqlStatement> statements,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(statements);

        var batch = statements.ToList();
        if (batch.Count == 0)
        {
            return [];
        }

        return await ExecuteHttpBatchAsync(batch, ct);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _httpClient.Dispose();
    }

    private async Task<IReadOnlyList<LibsqlQueryResult>> ExecuteHttpStatementsAsync(
        IReadOnlyList<LibsqlStatement> statements,
        CancellationToken ct)
    {
        var requests = statements.Select(CreateExecuteRequest).ToList();
        requests.Add(new HranaStreamRequest { Type = "close" });

        var pipeline = await SendPipelineAsync(requests, ct);
        return MapHttpResults(pipeline, statements.Count);
    }

    private async Task<IReadOnlyList<LibsqlQueryResult>> ExecuteHttpBatchAsync(
        IReadOnlyList<LibsqlStatement> statements,
        CancellationToken ct)
    {
        var requests = new List<HranaStreamRequest>
        {
            CreateBatchRequest(statements),
            new() { Type = "close" }
        };

        var pipeline = await SendPipelineAsync(requests, ct);
        return MapHttpBatchResults(pipeline, statements.Count);
    }

    private async Task<HranaPipelineResponse> SendPipelineAsync(
        IReadOnlyList<HranaStreamRequest> requests,
        CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var httpClient = _httpClient ?? throw new InvalidOperationException("HTTP client was not initialized.");

        var requestBody = new HranaPipelineRequest(requests);
        using var request = new HttpRequestMessage(HttpMethod.Post, "v2/pipeline")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(requestBody, HranaJsonSerializerContext.Default.HranaPipelineRequest),
                Encoding.UTF8,
                "application/json")
        };

        using var response = await httpClient.SendAsync(request, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new LibsqlException($"libSQL HTTP execution failed with status {(int)response.StatusCode}: {responseBody}");
        }

        var pipeline = JsonSerializer.Deserialize(
            responseBody,
            HranaJsonSerializerContext.Default.HranaPipelineResponse);
        if (pipeline is null)
        {
            throw new LibsqlException("libSQL HTTP response was empty.");
        }

        return pipeline;
    }

    private static HranaStreamRequest CreateExecuteRequest(LibsqlStatement statement)
    {
        return new HranaStreamRequest
        {
            Type = "execute",
            Stmt = CreateStatement(statement)
        };
    }

    private static HranaStreamRequest CreateBatchRequest(IReadOnlyList<LibsqlStatement> statements)
    {
        var steps = new List<HranaBatchStep>(statements.Count + 3)
        {
            new() { Stmt = CreateStatement("BEGIN IMMEDIATE") }
        };

        var previousStep = 0;
        foreach (var statement in statements)
        {
            steps.Add(new HranaBatchStep
            {
                Condition = HranaBatchCondition.Ok(previousStep),
                Stmt = CreateStatement(statement)
            });
            previousStep = steps.Count - 1;
        }

        steps.Add(new HranaBatchStep
        {
            Condition = HranaBatchCondition.Ok(previousStep),
            Stmt = CreateStatement("COMMIT")
        });
        var commitStep = steps.Count - 1;
        steps.Add(new HranaBatchStep
        {
            Condition = HranaBatchCondition.Not(HranaBatchCondition.Ok(commitStep)),
            Stmt = CreateStatement("ROLLBACK")
        });

        return new HranaStreamRequest
        {
            Type = "batch",
            Batch = new HranaBatch { Steps = steps }
        };
    }

    private static HranaStatement CreateStatement(string sql)
        => new() { Sql = sql, WantRows = false };

    private static HranaStatement CreateStatement(LibsqlStatement statement)
    {
        return new HranaStatement
        {
            Sql = statement.Sql,
            NamedArgs = LibsqlCommandHelpers.EnumerateParameters(statement.Parameters)
                .Select(pair => new HranaNamedArg(
                    LibsqlCommandHelpers.NormalizeParameterName(pair.Key)[1..],
                    HranaValue.FromClrValue(LibsqlCommandHelpers.NormalizeParameterValue(pair.Value))))
                .ToList(),
            WantRows = LibsqlCommandHelpers.ReturnsRows(statement.Sql)
        };
    }

    private static IReadOnlyList<LibsqlQueryResult> MapHttpResults(
        HranaPipelineResponse pipeline,
        int statementCount)
    {
        var results = new List<LibsqlQueryResult>(statementCount);

        for (var i = 0; i < pipeline.Results.Count; i++)
        {
            var streamResult = pipeline.Results[i];
            if (string.Equals(streamResult.Type, "error", StringComparison.OrdinalIgnoreCase))
            {
                throw new LibsqlException(streamResult.Error?.Message ?? "libSQL HTTP statement failed.");
            }
        }

        for (var i = 0; i < statementCount; i++)
        {
            var streamResult = pipeline.Results[i];
            var statementResult = streamResult.Response?.Result
                ?? throw new LibsqlException("libSQL HTTP response did not include a statement result.");
            results.Add(MapStatementResult(statementResult));
        }

        return results;
    }

    private static IReadOnlyList<LibsqlQueryResult> MapHttpBatchResults(
        HranaPipelineResponse pipeline,
        int statementCount)
    {
        foreach (var result in pipeline.Results)
        {
            if (string.Equals(result.Type, "error", StringComparison.OrdinalIgnoreCase))
            {
                throw new LibsqlException(result.Error?.Message ?? "libSQL HTTP batch failed.");
            }
        }

        var streamResult = pipeline.Results.FirstOrDefault()
            ?? throw new LibsqlException("libSQL HTTP response did not include a batch result.");
        var batchResult = streamResult.Response?.Result
            ?? throw new LibsqlException("libSQL HTTP response did not include a batch result.");
        var stepErrors = batchResult.StepErrors
            ?? throw new LibsqlException("libSQL HTTP batch response did not include step errors.");
        var stepResults = batchResult.StepResults
            ?? throw new LibsqlException("libSQL HTTP batch response did not include step results.");
        var expectedStepCount = statementCount + 3;
        if (stepErrors.Count < expectedStepCount || stepResults.Count < expectedStepCount)
        {
            throw new LibsqlException("libSQL HTTP batch response was incomplete.");
        }

        var stepError = stepErrors.FirstOrDefault(error => error is not null);
        if (stepError is not null)
        {
            throw new LibsqlException(stepError.Message);
        }

        var results = new List<LibsqlQueryResult>(statementCount);
        for (var i = 0; i < statementCount; i++)
        {
            var statementResult = stepResults[i + 1]
                ?? throw new LibsqlException("libSQL HTTP batch skipped a statement unexpectedly.");
            results.Add(MapStatementResult(statementResult));
        }

        return results;
    }

    private static LibsqlQueryResult MapStatementResult(HranaStatementResult result)
    {
        var columns = result.Cols
            .Select(column => column.Name ?? string.Empty)
            .ToList();
        var rows = new List<LibsqlRow>(result.Rows.Count);

        foreach (var row in result.Rows)
        {
            var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < columns.Count; i++)
            {
                values[columns[i]] = row[i].ToClrValue();
            }

            rows.Add(new LibsqlRow(columns, values));
        }

        var lastInsertRowId = long.TryParse(
            result.LastInsertRowId,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var parsedRowId)
            ? parsedRowId
            : (long?)null;

        return new LibsqlQueryResult(rows, checked((int)result.AffectedRowCount), lastInsertRowId);
    }

    private static bool IsHttpDataSource(string dataSource)
    {
        return dataSource.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || dataSource.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || dataSource.StartsWith("libsql://", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeHttpDataSource(string dataSource)
    {
        var trimmed = dataSource.Trim().TrimEnd('/');
        return trimmed.StartsWith("libsql://", StringComparison.OrdinalIgnoreCase)
            ? $"https://{trimmed["libsql://".Length..]}"
            : trimmed;
    }

    private sealed record HranaPipelineRequest(
        [property: JsonPropertyName("requests")] IReadOnlyList<HranaStreamRequest> Requests);

    private sealed class HranaPipelineResponse
    {
        [JsonPropertyName("results")]
        public List<HranaStreamResult> Results { get; set; } = [];
    }

    private sealed class HranaStreamRequest
    {
        [JsonPropertyName("type")]
        public string Type { get; init; } = string.Empty;

        [JsonPropertyName("stmt")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public HranaStatement? Stmt { get; init; }

        [JsonPropertyName("batch")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public HranaBatch? Batch { get; init; }
    }

    private sealed class HranaStreamResult
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("response")]
        public HranaStreamResponse? Response { get; set; }

        [JsonPropertyName("error")]
        public HranaError? Error { get; set; }
    }

    private sealed class HranaStreamResponse
    {
        [JsonPropertyName("result")]
        public HranaStatementResult? Result { get; set; }
    }

    private sealed class HranaError
    {
        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;
    }

    private sealed class HranaStatement
    {
        [JsonPropertyName("sql")]
        public string Sql { get; init; } = string.Empty;

        [JsonPropertyName("named_args")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<HranaNamedArg>? NamedArgs { get; init; }

        [JsonPropertyName("want_rows")]
        public bool WantRows { get; init; }
    }

    private sealed class HranaBatch
    {
        [JsonPropertyName("steps")]
        public List<HranaBatchStep> Steps { get; init; } = [];
    }

    private sealed class HranaBatchStep
    {
        [JsonPropertyName("condition")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public HranaBatchCondition? Condition { get; init; }

        [JsonPropertyName("stmt")]
        public HranaStatement Stmt { get; init; } = new();
    }

    private sealed class HranaBatchCondition
    {
        [JsonPropertyName("type")]
        public string Type { get; init; } = string.Empty;

        [JsonPropertyName("step")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? Step { get; init; }

        [JsonPropertyName("cond")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public HranaBatchCondition? Condition { get; init; }

        public static HranaBatchCondition Ok(int step) => new() { Type = "ok", Step = step };

        public static HranaBatchCondition Not(HranaBatchCondition condition)
            => new() { Type = "not", Condition = condition };
    }

    private sealed record HranaNamedArg(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("value")] HranaValue Value);

    private sealed class HranaStatementResult
    {
        [JsonPropertyName("cols")]
        public List<HranaColumn> Cols { get; set; } = [];

        [JsonPropertyName("rows")]
        public List<List<HranaValue>> Rows { get; set; } = [];

        [JsonPropertyName("affected_row_count")]
        public long AffectedRowCount { get; set; }

        [JsonPropertyName("last_insert_rowid")]
        public string? LastInsertRowId { get; set; }

        [JsonPropertyName("step_results")]
        public List<HranaStatementResult?>? StepResults { get; set; }

        [JsonPropertyName("step_errors")]
        public List<HranaError?>? StepErrors { get; set; }
    }

    private sealed class HranaColumn
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    [JsonConverter(typeof(HranaValueJsonConverter))]
    private sealed class HranaValue
    {
        public string Type { get; init; } = "null";
        public object? Value { get; init; }

        public static HranaValue FromClrValue(object? value)
        {
            return value switch
            {
                null or DBNull => new HranaValue(),
                byte[] bytes => new HranaValue { Type = "blob", Value = bytes },
                bool boolValue => new HranaValue { Type = "integer", Value = boolValue ? "1" : "0" },
                sbyte or byte or short or ushort or int or uint or long or ulong => new HranaValue
                {
                    Type = "integer",
                    Value = Convert.ToString(value, CultureInfo.InvariantCulture)
                },
                float or double or decimal => new HranaValue
                {
                    Type = "float",
                    Value = Convert.ToDouble(value, CultureInfo.InvariantCulture)
                },
                _ => new HranaValue
                {
                    Type = "text",
                    Value = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
                }
            };
        }

        public object? ToClrValue()
        {
            return Type switch
            {
                "null" => null,
                "integer" => Value is string integerText
                    && long.TryParse(integerText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer)
                        ? integer
                        : 0L,
                "float" => Value is double doubleValue
                    ? doubleValue
                    : Convert.ToDouble(Value, CultureInfo.InvariantCulture),
                "text" => Value?.ToString(),
                "blob" => Value is byte[] bytes ? bytes : [],
                _ => Value
            };
        }
    }

    private sealed class HranaValueJsonConverter : JsonConverter<HranaValue>
    {
        public override HranaValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            string type = "null";
            object? value = null;

            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException("Expected Hrana value object.");
            }

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    return new HranaValue { Type = type, Value = value };
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    throw new JsonException("Expected Hrana value property.");
                }

                var propertyName = reader.GetString();
                reader.Read();

                switch (propertyName)
                {
                    case "type":
                        type = reader.GetString() ?? "null";
                        break;
                    case "value":
                        value = reader.TokenType switch
                        {
                            JsonTokenType.Number => reader.GetDouble(),
                            JsonTokenType.String => reader.GetString(),
                            JsonTokenType.Null => null,
                            _ => throw new JsonException("Unsupported Hrana value token.")
                        };
                        break;
                    case "base64":
                        value = Convert.FromBase64String(reader.GetString() ?? string.Empty);
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            throw new JsonException("Unexpected end of Hrana value.");
        }

        public override void Write(Utf8JsonWriter writer, HranaValue value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteString("type", value.Type);

            switch (value.Type)
            {
                case "null":
                    break;
                case "integer":
                case "text":
                    writer.WriteString("value", Convert.ToString(value.Value, CultureInfo.InvariantCulture));
                    break;
                case "float":
                    writer.WriteNumber("value", Convert.ToDouble(value.Value, CultureInfo.InvariantCulture));
                    break;
                case "blob":
                    writer.WriteString("base64", Convert.ToBase64String((byte[])value.Value!));
                    break;
                default:
                    throw new JsonException($"Unsupported Hrana value type '{value.Type}'.");
            }

            writer.WriteEndObject();
        }
    }

    [JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonSerializable(typeof(HranaPipelineRequest))]
    [JsonSerializable(typeof(HranaPipelineResponse))]
    private sealed partial class HranaJsonSerializerContext : JsonSerializerContext;
}
