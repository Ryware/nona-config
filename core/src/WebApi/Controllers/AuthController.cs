using System.Text.Json;
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

    [HttpGet("cli/login")]
    [AllowAnonymous]
    public IActionResult CliLogin([FromQuery] string state, [FromQuery(Name = "redirect_uri")] string redirectUri)
    {
        if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out var uri) ||
            uri.Scheme != "http" ||
            (!string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(uri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)))
        {
            return BadRequest("redirect_uri must be an http://localhost URL.");
        }

        return Content(BuildCliLoginHtml(state, redirectUri), "text/html");
    }

    private static string BuildCliLoginHtml(string state, string redirectUri)
    {
        var stateJson = JsonSerializer.Serialize(state);
        var redirectUriJson = JsonSerializer.Serialize(redirectUri);

        return $"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="UTF-8">
                <meta name="viewport" content="width=device-width, initial-scale=1.0">
                <title>Nona CLI Login</title>
                <style>
                    * {{ box-sizing: border-box; margin: 0; padding: 0; }}
                    body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background: #f5f5f5; display: flex; align-items: center; justify-content: center; min-height: 100vh; }}
                    .card {{ background: #fff; border-radius: 12px; padding: 40px; width: 100%; max-width: 380px; box-shadow: 0 4px 16px rgba(0,0,0,.1); }}
                    h1 {{ font-size: 22px; font-weight: 600; margin-bottom: 6px; }}
                    .sub {{ color: #666; font-size: 14px; margin-bottom: 28px; }}
                    label {{ display: block; font-size: 13px; font-weight: 500; color: #333; margin-bottom: 6px; }}
                    input {{ width: 100%; padding: 10px 12px; border: 1px solid #ddd; border-radius: 8px; font-size: 14px; margin-bottom: 16px; outline: none; transition: border-color .15s; }}
                    input:focus {{ border-color: #111; }}
                    button {{ width: 100%; padding: 11px; background: #111; color: #fff; border: none; border-radius: 8px; font-size: 14px; font-weight: 500; cursor: pointer; transition: background .15s; }}
                    button:hover:not(:disabled) {{ background: #333; }}
                    button:disabled {{ background: #999; cursor: default; }}
                    .error {{ color: #c00; font-size: 13px; margin-top: 14px; min-height: 18px; }}
                </style>
            </head>
            <body>
                <div class="card">
                    <h1>Nona CLI</h1>
                    <p class="sub">Sign in to continue in your terminal.</p>
                    <form id="form">
                        <label for="email">Email</label>
                        <input type="email" id="email" autocomplete="email" required placeholder="you@example.com">
                        <label for="password">Password</label>
                        <input type="password" id="password" autocomplete="current-password" required>
                        <button type="submit" id="btn">Sign in</button>
                        <p class="error" id="error"></p>
                    </form>
                </div>
                <script>
                    const STATE = {stateJson};
                    const REDIRECT_URI = {redirectUriJson};

                    document.getElementById('form').addEventListener('submit', async function(e) {{
                        e.preventDefault();
                        const btn = document.getElementById('btn');
                        const errorEl = document.getElementById('error');
                        btn.disabled = true;
                        btn.textContent = 'Signing in…';
                        errorEl.textContent = '';

                        const email = document.getElementById('email').value;
                        const password = document.getElementById('password').value;

                        try {{
                            const res = await fetch('/auth/login', {{
                                method: 'POST',
                                headers: {{ 'Content-Type': 'application/json' }},
                                body: JSON.stringify({{ email, password }})
                            }});

                            if (res.ok) {{
                                const data = await res.json();
                                const params = new URLSearchParams({{
                                    token: data.token,
                                    username: data.username || '',
                                    role: data.role || '',
                                    expires_at: data.expiresAt || '',
                                    state: STATE
                                }});
                                window.location.href = REDIRECT_URI + '?' + params.toString();
                            }} else {{
                                const data = await res.json().catch(() => ({{}}));
                                errorEl.textContent = data.error || (res.status === 401 ? 'Invalid email or password.' : 'Login failed. Please try again.');
                                btn.disabled = false;
                                btn.textContent = 'Sign in';
                            }}
                        }} catch (_) {{
                            errorEl.textContent = 'Connection error. Please try again.';
                            btn.disabled = false;
                            btn.textContent = 'Sign in';
                        }}
                    }});
                </script>
            </body>
            </html>
            """;
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
