using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Nona.Libsql;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Nona.WebApi.Controllers.Internal;

[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("internal/libsql/v2")]
public sealed class InternalLibsqlController : ControllerBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly LibsqlOptions _options;

    public InternalLibsqlController(IServiceProvider serviceProvider, IOptions<LibsqlOptions> options)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
    }

    [HttpPost("pipeline")]
    public async Task<IActionResult> ExecutePipeline(CancellationToken cancellationToken)
    {
        if (!_options.EnableLocalReplica ||
            !_options.LocalReplicaRole.Equals("Primary", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound();
        }

        if (!TryGetBearerToken(out var token) ||
            !string.Equals(token, _options.AuthToken, StringComparison.Ordinal))
        {
            return Unauthorized();
        }

        var client = _serviceProvider.GetService<LibsqlMirroredLocalDatabaseClient>();
        if (client is null)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Primary local replica client is not available." });
        }

        using var document = await JsonDocument.ParseAsync(Request.Body, cancellationToken: cancellationToken);
        if (!document.RootElement.TryGetProperty("requests", out var requestsElement) ||
            requestsElement.ValueKind != JsonValueKind.Array)
        {
            return BadRequest(new { error = "libSQL request did not contain a valid requests array." });
        }

        var results = new List<object?>();

        foreach (var requestElement in requestsElement.EnumerateArray())
        {
            if (!requestElement.TryGetProperty("type", out var typeElement))
            {
                results.Add(new { type = "error", error = new { message = "libSQL request type was missing." } });
                continue;
            }

            var requestType = typeElement.GetString();
            if (string.Equals(requestType, "close", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(new { type = "ok", response = new { type = "close" } });
                continue;
            }

            if (!string.Equals(requestType, "execute", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(new { type = "error", error = new { message = $"Unsupported request type '{requestType}'." } });
                continue;
            }

            try
            {
                var statement = ParseStatement(requestElement);
                var result = await client.ExecuteLocalBatchAsync([statement], cancellationToken);
                results.Add(new
                {
                    type = "ok",
                    response = new
                    {
                        type = "execute",
                        result = CreateExecuteResult(result[0])
                    }
                });
            }
            catch (Exception ex)
            {
                results.Add(new { type = "error", error = new { message = ex.Message } });
            }
        }

        return Ok(new
        {
            baton = (string?)null,
            base_url = (string?)null,
            results
        });
    }

    private bool TryGetBearerToken(out string token)
    {
        token = string.Empty;

        if (!Request.Headers.TryGetValue("Authorization", out var authorizationValues))
        {
            return false;
        }

        var header = authorizationValues.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        token = header["Bearer ".Length..].Trim();
        return !string.IsNullOrWhiteSpace(token);
    }

    private static LibsqlStatement ParseStatement(JsonElement requestElement)
    {
        if (!requestElement.TryGetProperty("stmt", out var statementElement) ||
            statementElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("libSQL execute request did not contain a valid stmt payload.");
        }

        var sql = statementElement.GetProperty("sql").GetString();
        if (string.IsNullOrWhiteSpace(sql))
        {
            throw new InvalidOperationException("libSQL execute request did not contain SQL.");
        }

        var parameters = ParseNamedArgs(statementElement);
        return new LibsqlStatement(sql, parameters);
    }

    private static IReadOnlyDictionary<string, object?>? ParseNamedArgs(JsonElement statementElement)
    {
        if (!statementElement.TryGetProperty("named_args", out var namedArgsElement) ||
            namedArgsElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var parameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var argElement in namedArgsElement.EnumerateArray())
        {
            var name = argElement.GetProperty("name").GetString();
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            parameters[name] = ConvertArgument(argElement.GetProperty("value"));
        }

        return parameters;
    }

    private static object CreateExecuteResult(LibsqlQueryResult result)
    {
        return new
        {
            cols = result.Rows.Count == 0
                ? Array.Empty<object>()
                : result.Rows[0].Columns.Select(column => new { name = column }).ToArray(),
            rows = result.Rows.Select(row => row.Columns.Select(column => CreateResultValue(row.GetValue(column))).ToArray()).ToArray(),
            affected_row_count = result.AffectedRowCount,
            last_insert_rowid = result.LastInsertRowId?.ToString(CultureInfo.InvariantCulture),
            replication_index = "1"
        };
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
}
