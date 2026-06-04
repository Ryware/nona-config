using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nona.Application.Admin.ApiKeys.Commands;
using Nona.Application.Admin.ApiKeys.DTOs;
using Nona.Application.Admin.ApiKeys.Queries;

namespace Nona.WebApi.Controllers.Admin;

[ApiController]
[Authorize]
[Route("admin/projects/{projectId}/api-keys")]
public class AdminApiKeysController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ApiKeyDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListApiKeys(string projectId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ListApiKeysQuery(projectId), cancellationToken);

        if (!result.Success)
        {
            return result.Error switch
            {
                "Project not found" => NotFound(new { error = result.Error }),
                "Access denied" => Forbid(),
                _ => BadRequest(new { error = result.Error })
            };
        }

        return Ok(result.ApiKeys);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiKeyDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateApiKey(
        string projectId,
        [FromBody] CreateApiKeyRequest request,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new CreateApiKeyCommand(projectId, request.Name, request.Environment, request.Scope),
            cancellationToken);

        if (!result.Success)
        {
            return result.Error switch
            {
                "Project not found" or "Environment not found" => NotFound(new { error = result.Error }),
                "Access denied" => Forbid(),
                _ => BadRequest(new { error = result.Error })
            };
        }

        return CreatedAtAction(nameof(ListApiKeys), new { projectId }, result.ApiKey);
    }

    [HttpDelete("{apiKeyId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteApiKey(
        string projectId,
        long apiKeyId,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new DeleteApiKeyCommand(projectId, apiKeyId), cancellationToken);

        if (!result.Success)
        {
            return result.Error switch
            {
                "Project not found" or "API key not found" => NotFound(new { error = result.Error }),
                "Access denied" => Forbid(),
                _ => BadRequest(new { error = result.Error })
            };
        }

        return NoContent();
    }
}
