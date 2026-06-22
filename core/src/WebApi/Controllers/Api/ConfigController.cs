using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nona.Application.Api.ConfigEntries.Queries;
using Nona.WebApi.Authentication;

namespace Nona.WebApi.Controllers.Api;

[ApiController]
[Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
[Route("api/{environmentId}")]
[Tags("Config API")]
public class ConfigController(IMediator mediator) : ControllerBase
{
    [HttpGet("{key}")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK, "text/plain")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetConfigValue(string environmentId, string key, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetConfigEntryValueQuery(environmentId, key), cancellationToken);

        if (!result.Success)
        {
            return result.Error switch
            {
                "API key is required" or "Invalid API key" => Unauthorized(new { error = result.Error }),
                _ => NotFound(new { error = result.Error })
            };
        }

        Response.Headers["ContentType"] = result.ContentType!;
        return Content(result.Value!, "text/plain; charset=utf-8");
    }
}
