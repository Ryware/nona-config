using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nona.Application.Admin.Dashboard.DTOs;
using Nona.Application.Admin.Dashboard.Queries;

namespace Nona.WebApi.Controllers.Admin;

[ApiController]
[Authorize]
[Route("admin/dashboard")]

public class DashboardController(IMediator mediator) : ControllerBase
{
    [HttpGet("counts")]
    [ProducesResponseType(typeof(DashboardCountDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboardCounts(CancellationToken cancellationToken)
    {
        var counts = await mediator.Send(new GetDashboardCountsQuery(), cancellationToken);

        return Ok(counts);
    }
}
