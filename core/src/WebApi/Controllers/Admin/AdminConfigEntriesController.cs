using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nona.Application.Admin.Common;
using Nona.Application.Admin.ConfigEntries.Commands;
using Nona.Application.Admin.ConfigEntries.DTOs;
using Nona.Application.Admin.ConfigEntries.Queries;

namespace Nona.WebApi.Controllers.Admin;

[ApiController]
[Authorize]
[Route("admin/projects/{projectId}/environments/{environmentId}/config")]
public class AdminConfigEntriesController(IMediator mediator) : ControllerBase
{
    [HttpGet("{key}")]
    [ProducesResponseType(typeof(ConfigEntryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetConfigEntry(string projectId, string environmentId, string key, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetConfigEntryQuery(projectId, environmentId, key), cancellationToken);
        if (!result.Success)
            return NotFound(new { error = result.Error });

        return Ok(result.ConfigEntry);
    }

    [HttpPut("{key}")]
    [ProducesResponseType(typeof(ConfigEntryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpsertConfigEntry(string projectId, string environmentId, string key, [FromBody] UpsertConfigEntryRequest request, CancellationToken cancellationToken)
    {
        if (!ValidationHelpers.IsValidKey(key))
            return BadRequest(new { error = "Key must be non-empty and contain no spaces" });

        var result = await mediator.Send(new UpsertConfigEntryCommand(projectId, environmentId, key, request.Value, request.ContentType, request.Scope), cancellationToken);
        if (!result.Success)
        {
            return result.Error switch
            {
                "Project not found" or "Environment not found" => NotFound(new { error = result.Error }),
                _ => BadRequest(new { error = result.Error })
            };
        }

        return Ok(result.ConfigEntry);
    }

    [HttpDelete("{key}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteConfigEntry(string projectId, string environmentId, string key, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new DeleteConfigEntryCommand(projectId, environmentId, key), cancellationToken);
        if (!result.Success)
            return NotFound(new { error = result.Error });

        return NoContent();
    }
}
