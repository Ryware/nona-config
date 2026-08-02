namespace Nona.Application.Admin.AuditLogs.DTOs;

public record AuditLogPageDto(
    IReadOnlyList<AuditLogDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    IReadOnlyList<string> Actions,
    IReadOnlyList<string> Environments);
