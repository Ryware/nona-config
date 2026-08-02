using Nona.Domain.Entities;

namespace Nona.Domain.Interfaces;

public sealed record AuditLogFilter(
    string? Search = null,
    string? Action = null,
    string? Environment = null,
    DateTime? CreatedFrom = null,
    DateTime? CreatedToExclusive = null);

public sealed record AuditLogPageRequest(
    AuditLogFilter Filter,
    long Offset,
    int Limit);

public sealed record AuditLogPageResult(
    IReadOnlyList<AuditLogEntry> Items,
    int TotalCount,
    IReadOnlyList<string> Actions,
    IReadOnlyList<string> Environments);
