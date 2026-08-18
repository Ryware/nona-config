using Nona.Domain.Entities;

namespace Nona.Domain.Interfaces;

public sealed record AuditLogFilter(
    string? Search = null,
    string? Action = null,
    string? Environment = null,
    DateTime? CreatedFrom = null,
    DateTime? CreatedToExclusive = null)
{
    public const string GlobalScopeEnvironment = "__global__";

    public static bool IsGlobalScopeEnvironment(string? environment)
    {
        return string.Equals(
            environment?.Trim(),
            GlobalScopeEnvironment,
            StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record AuditLogPageRequest(
    AuditLogFilter Filter,
    long Offset,
    int Limit);

public sealed record AuditLogPageResult(
    IReadOnlyList<AuditLogEntry> Items,
    int TotalCount,
    IReadOnlyList<string> Actions,
    IReadOnlyList<string> Environments);

public sealed record AuditLogBatchRequest(
    AuditLogFilter Filter,
    DateTime? BeforeCreatedAt,
    long? BeforeId,
    int Limit);
