using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nona.Application.Admin.Environments.Commands;
using Nona.Application.Admin.Environments.DTOs;
using Nona.Application.Admin.Environments.Queries;

namespace Nona.WebApi.Controllers.Admin;

[ApiController]
[Authorize]
[Route("admin/projects/{projectId}/environments")]
public class AdminEnvironmentsController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(EnvironmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateEnvironment(string projectId, [FromBody] CreateEnvironmentRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new CreateEnvironmentCommand(projectId, request.Name), cancellationToken);

        if (!result.Success)
        {
            return result.Error switch
            {
                "Project not found" => NotFound(new { error = result.Error }),
                "Environment already exists" => Conflict(new { error = result.Error }),
                _ => BadRequest(new { error = result.Error })
            };
        }

        return CreatedAtAction(nameof(ListEnvironments), new { projectId }, result.Environment);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<EnvironmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListEnvironments(string projectId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ListEnvironmentsQuery(projectId), cancellationToken);

        if (!result.Success)
            return NotFound(new { error = result.Error });

        return Ok(result.Environments);
    }

    [HttpDelete("{environmentId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteEnvironment(string projectId, string environmentId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new DeleteEnvironmentCommand(projectId, environmentId), cancellationToken);

        if (!result.Success)
            return NotFound(new { error = result.Error });

        return NoContent();
    }
}
