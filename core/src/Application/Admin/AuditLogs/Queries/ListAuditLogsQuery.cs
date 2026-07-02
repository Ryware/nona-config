using Mediator;
using Nona.Application.Admin.AuditLogs.DTOs;
using Nona.Domain.Interfaces;

namespace Nona.Application.Admin.AuditLogs.Queries;

public record ListAuditLogsQuery : IRequest<IReadOnlyList<AuditLogDto>>;

public class ListAuditLogsQueryHandler(IAuditLogRepository auditLogRepository)
    : IRequestHandler<ListAuditLogsQuery, IReadOnlyList<AuditLogDto>>
{
    public async ValueTask<IReadOnlyList<AuditLogDto>> Handle(ListAuditLogsQuery request, CancellationToken cancellationToken)
    {
        var entries = await auditLogRepository.ListAsync(cancellationToken);

        return entries
            .Select(entry => new AuditLogDto(
                entry.Id,
                entry.Actor,
                entry.ActorIsSystem,
                entry.Action,
                entry.Target,
                entry.Project,
                entry.Environment,
                entry.CreatedAt))
            .ToList();
    }
}
