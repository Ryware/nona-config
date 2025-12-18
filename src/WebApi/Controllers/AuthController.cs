using MediatR;
using Microsoft.AspNetCore.Mvc;
using Nona.Application.Auth.Commands;
using Nona.Application.Auth.DTOs;

namespace Nona.WebApi.Controllers;

[ApiController]
[Route("auth")]
[Tags("Auth")]
public class AuthController(IMediator mediator) : ControllerBase
{
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new LoginCommand(request.Username, request.Password), cancellationToken);

        if (!result.Success)
            return Unauthorized(new { error = result.Error });

        return Ok(result.Response);
    }
}
