using Mediator;
using Nona.Application.Admin.AuditLogs.DTOs;
using Nona.Domain.Interfaces;

namespace Nona.Application.Admin.AuditLogs.Queries;

public record ListAuditLogsQuery(
    int Page = 1,
    int PageSize = 25,
    string? Search = null,
    string? Action = null,
    string? Environment = null,
    DateOnly? DateFrom = null,
    DateOnly? DateTo = null) : IRequest<AuditLogPageDto>;

public class ListAuditLogsQueryHandler(IAuditLogRepository auditLogRepository)
    : IRequestHandler<ListAuditLogsQuery, AuditLogPageDto>
{
    public const int DefaultPageSize = 25;
    public const int MaximumPageSize = 100;

    public async ValueTask<AuditLogPageDto> Handle(ListAuditLogsQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, MaximumPageSize);
        var filter = AuditLogQueryFilter.Create(
            request.Search,
            request.Action,
            request.Environment,
            request.DateFrom,
            request.DateTo);
        var result = await auditLogRepository.ListAsync(
            new AuditLogPageRequest(filter, (long)(page - 1) * pageSize, pageSize),
            cancellationToken);

        var items = result.Items
            .Select(AuditLogDto.FromEntry)
            .ToList();

        return new AuditLogPageDto(
            items,
            page,
            pageSize,
            result.TotalCount,
            result.TotalCount == 0 ? 0 : (int)Math.Ceiling((double)result.TotalCount / pageSize),
            result.Actions,
            result.Environments);
    }
}
