using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nona.Application.Admin.AuditLogs.DTOs;
using Nona.Application.Admin.AuditLogs.Queries;

namespace Nona.WebApi.Controllers.Admin;

[ApiController]
[Authorize]
[Route("admin/audit-logs")]
public class AdminAuditLogsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AuditLogDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAuditLogs(CancellationToken cancellationToken)
    {
        var logs = await mediator.Send(new ListAuditLogsQuery(), cancellationToken);
        return Ok(logs);
    }
}
