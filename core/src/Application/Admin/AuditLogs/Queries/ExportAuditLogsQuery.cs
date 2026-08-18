using System.Runtime.CompilerServices;
using Mediator;
using Nona.Application.Admin.AuditLogs.DTOs;
using Nona.Domain.Interfaces;

namespace Nona.Application.Admin.AuditLogs.Queries;

public record ExportAuditLogsQuery(
    string? Search = null,
    string? Action = null,
    string? Environment = null,
    DateOnly? DateFrom = null,
    DateOnly? DateTo = null) : IStreamQuery<AuditLogDto>;

public sealed class ExportAuditLogsQueryHandler(IAuditLogRepository auditLogRepository)
    : IStreamQueryHandler<ExportAuditLogsQuery, AuditLogDto>
{
    public const int BatchSize = 500;

    public async IAsyncEnumerable<AuditLogDto> Handle(
        ExportAuditLogsQuery request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var filter = AuditLogQueryFilter.Create(
            request.Search,
            request.Action,
            request.Environment,
            request.DateFrom,
            request.DateTo);
        DateTime? beforeCreatedAt = null;
        long? beforeId = null;

        while (true)
        {
            var entries = await auditLogRepository.ListBatchAsync(
                new AuditLogBatchRequest(filter, beforeCreatedAt, beforeId, BatchSize),
                cancellationToken);

            foreach (var entry in entries)
            {
                yield return AuditLogDto.FromEntry(entry);
            }

            if (entries.Count < BatchSize)
            {
                yield break;
            }

            var last = entries[^1];
            beforeCreatedAt = last.CreatedAt;
            beforeId = last.Id;
        }
    }
}
