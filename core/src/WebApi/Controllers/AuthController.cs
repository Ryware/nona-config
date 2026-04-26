using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nona.Application.Auth;
using Nona.Application.Admin.Users.Queries;
using Nona.Application.Auth.Commands;
using Nona.Application.Auth.DTOs;
using Nona.Application.Auth.Queries;
using Nona.Application.Common.Interfaces;

namespace Nona.WebApi.Controllers;

[ApiController]
[Route("auth")]
[Tags("Auth")]
public class AuthController(IMediator mediator, ISsoPublicConfigurationProvider ssoPublicConfigurationProvider) : ControllerBase
{
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new LoginCommand(request.Email, request.Password), cancellationToken);

        if (!result.Success)
            return Unauthorized(new { error = result.Error });

        return Ok(result.Response);
    }

    [HttpGet("sso/config")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(SsoPublicConfigResponse), StatusCodes.Status200OK)]
    public IActionResult GetSsoConfiguration()
    {
        return Ok(ssoPublicConfigurationProvider.GetConfiguration());
    }

    [HttpPost("sso/google")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> LoginWithGoogle([FromBody] SsoLoginRequest request, CancellationToken cancellationToken)
    {
        return await LoginWithSsoAsync(SsoProviders.Google, request, cancellationToken);
    }

    [HttpPost("sso/microsoft")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> LoginWithMicrosoft([FromBody] SsoLoginRequest request, CancellationToken cancellationToken)
    {
        return await LoginWithSsoAsync(SsoProviders.Microsoft, request, cancellationToken);
    }

    [HttpGet("first-time")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckIfAnyUsersExist()
    {
        return Ok(await mediator.Send(new AnyUsersQuery()));
    }

    [HttpPost("register")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Register([FromBody] RegisterCommand command)
    {
        return Ok(await mediator.Send(command));
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RequestPasswordReset([FromBody] RequestPasswordResetCommand command, CancellationToken cancellationToken)
    {
        await mediator.Send(command, cancellationToken);
        return NoContent();
    }

    [HttpGet("invitations/{token}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(InvitationDetailsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetInvitation(string token, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetInvitationQuery(token), cancellationToken);
        if (!result.Success)
            return NotFound(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(result.Invitation);
    }

    [HttpPost("invitations/{token}/password")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CompleteInvitationWithPassword(
        string token,
        [FromBody] CompleteInvitationPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new CompleteInvitationWithPasswordCommand(token, request.NewPassword), cancellationToken);

        if (!result.Success)
        {
            return result.ErrorCode == AuthErrorCodes.InvitationInvalidOrUsed
                ? NotFound(new { error = result.Error, errorCode = result.ErrorCode })
                : BadRequest(new { error = result.Error, errorCode = result.ErrorCode });
        }

        return Ok(result.Response);
    }

    [HttpPost("invitations/{token}/sso/{provider}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CompleteInvitationWithSso(
        string token,
        string provider,
        [FromBody] SsoLoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new CompleteInvitationWithSsoCommand(token, provider, request.IdToken), cancellationToken);

        if (!result.Success)
        {
            if (result.ErrorCode == AuthErrorCodes.InvitationInvalidOrUsed)
                return NotFound(new { error = result.Error, errorCode = result.ErrorCode });

            return Unauthorized(new { error = result.Error, errorCode = result.ErrorCode });
        }

        return Ok(result.Response);
    }

    private async Task<IActionResult> LoginWithSsoAsync(string provider, SsoLoginRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new LoginWithSsoCommand(provider, request.IdToken), cancellationToken);

        if (!result.Success)
        {
            return Unauthorized(new { error = result.Error, errorCode = result.ErrorCode });
        }

        return Ok(result.Response);
    }
}
