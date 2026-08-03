using System.Text;
using System.Text.Json;
using Nona.Application.Admin.AuditLogs.DTOs;
using Nona.WebApi.Serialization;

namespace Nona.WebApi.Endpoints;

internal sealed record AuditLogExportRecord(
    long Id,
    string Time,
    string Actor,
    string ActionKind,
    string Action,
    string Target,
    string Environment,
    string SysId,
    string? Project);

internal static class AuditLogExportWriter
{
    private const string CsvHeader = "Time,Actor,ActionKind,Action,Target,Environment,SysID,Project";
    private static readonly byte[] JsonArrayStart = "["u8.ToArray();
    private static readonly byte[] JsonArraySeparator = ","u8.ToArray();
    private static readonly byte[] JsonArrayEnd = "]"u8.ToArray();

    public static async Task WriteCsvAsync(
        Stream output,
        IAsyncEnumerable<AuditLogDto> logs,
        CancellationToken cancellationToken)
    {
        await using var writer = new StreamWriter(
            output,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            bufferSize: 16 * 1024,
            leaveOpen: true);
        await writer.WriteLineAsync(CsvHeader.AsMemory(), cancellationToken);

        await foreach (var log in logs.WithCancellation(cancellationToken))
        {
            var record = ToExportRecord(log);
            var row = string.Join(",",
                CsvCell(record.Time),
                CsvCell(record.Actor),
                CsvCell(record.ActionKind),
                CsvCell(record.Action),
                CsvCell(record.Target),
                CsvCell(record.Environment),
                CsvCell(record.SysId),
                CsvCell(record.Project ?? string.Empty));
            await writer.WriteLineAsync(row.AsMemory(), cancellationToken);
        }

        await writer.FlushAsync(cancellationToken);
    }

    public static async Task WriteJsonAsync(
        Stream output,
        IAsyncEnumerable<AuditLogDto> logs,
        CancellationToken cancellationToken)
    {
        await output.WriteAsync(JsonArrayStart, cancellationToken);
        var first = true;

        await foreach (var log in logs.WithCancellation(cancellationToken))
        {
            if (!first)
            {
                await output.WriteAsync(JsonArraySeparator, cancellationToken);
            }

            await JsonSerializer.SerializeAsync(
                output,
                ToExportRecord(log),
                NonaJsonSerializerContext.Default.AuditLogExportRecord,
                cancellationToken);
            first = false;
        }

        await output.WriteAsync(JsonArrayEnd, cancellationToken);
    }

    private static AuditLogExportRecord ToExportRecord(AuditLogDto log)
    {
        var id = log.Id.ToString();
        return new AuditLogExportRecord(
            log.Id,
            log.CreatedAt.ToUniversalTime().ToString("O"),
            log.ActorIsSystem ? "System" : log.Actor,
            log.ActionKind,
            ResolveActionLabel(log.Action),
            log.Target,
            EnvironmentLabel(log.Environment),
            id[..Math.Min(8, id.Length)].ToUpperInvariant(),
            log.Project);
    }

    private static string ResolveActionLabel(string action)
    {
        var normalized = action.ToLowerInvariant().Replace(' ', '_').Replace('-', '_');
        return normalized switch
        {
            "create_project" or "created_project" => "Created Project",
            "update_project" or "updated_project" => "Updated Project",
            "invite_user" or "invited_user" => "Invited User",
            "delete_key" or "deleted_key" => "Deleted Key",
            "auto_scaling" => "Auto-Scaling",
            "create_config_entry" or "created_config_entry" or "create_entry" or "created_entry" => "Created Parameter",
            "update_config_entry" or "updated_config_entry" or "update_entry" or "updated_entry" => "Updated Parameter",
            "delete_config_entry" or "deleted_config_entry" or "delete_entry" or "deleted_entry" => "Deleted Parameter",
            _ => action
        };
    }

    private static string EnvironmentLabel(string? environment)
    {
        return string.IsNullOrEmpty(environment)
            ? "Global Scope"
            : char.ToUpperInvariant(environment[0]) + environment[1..];
    }

    private static string CsvCell(string value)
    {
        var safeValue = RequiresSpreadsheetNeutralization(value)
            ? $"'{value}"
            : value;
        return $"\"{safeValue.Replace("\"", "\"\"")}\"";
    }

    private static bool RequiresSpreadsheetNeutralization(string value)
    {
        return value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r' or '\n';
    }
}
