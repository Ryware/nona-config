using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nona.Application.Admin.Users.Commands;
using Nona.Application.Admin.Users.DTOs;
using Nona.Application.Admin.Users.Queries;

namespace Nona.WebApi.Controllers.Admin;

[ApiController]
[Authorize]
[Route("admin/users")]
public class AdminUsersController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new CreateUserCommand(request.Username, request.Password, request.Role, request.Scope), cancellationToken);

        if (!result.Success)
        {
            return result.Error == "User already exists"
                ? Conflict(new { error = result.Error })
                : BadRequest(new { error = result.Error });
        }

        return CreatedAtAction(nameof(GetUser), new { username = request.Username }, result.User);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<UserDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListUsers(CancellationToken cancellationToken)
    {
        var users = await mediator.Send(new ListUsersQuery(), cancellationToken);

        return Ok(users);
    }

    [HttpGet("{username}")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUser(string username, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetUserQuery(username), cancellationToken);

        if (!result.Success)
            return NotFound(new { error = result.Error });

        return Ok(result.User);
    }

    [HttpPut("{username}")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateUser(string username, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new UpdateUserCommand(username, request.Password, request.Role, request.Scope), cancellationToken);

        if (!result.Success)
        {
            return result.Error == "User not found"
                ? NotFound(new { error = result.Error })
                : BadRequest(new { error = result.Error });
        }

        return Ok(result.User);
    }

    [HttpDelete("{username}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteUser(string username, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new DeleteUserCommand(username), cancellationToken);

        if (!result.Success)
            return NotFound(new { error = result.Error });

        return NoContent();
    }

    [HttpGet("{username}/projects")]
    [ProducesResponseType(typeof(IReadOnlyList<ProjectAccessDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserProjects(string username, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetUserProjectsQuery(username), cancellationToken);

        if (!result.Success)
            return NotFound(new { error = result.Error });

        return Ok(result.Projects);
    }

    [HttpPut("{username}/projects/{projectName}")]
    [ProducesResponseType(typeof(ProjectAccessDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetProjectAccess(string username, string projectName, [FromBody] ProjectAccessRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new SetProjectAccessCommand(username, projectName, request.Role), cancellationToken);

        if (!result.Success)
        {
            return result.Error switch
            {
                "User not found" or "Project not found" => NotFound(new { error = result.Error }),
                _ => BadRequest(new { error = result.Error })
            };
        }

        return Ok(result.ProjectAccess);
    }

    [HttpDelete("{username}/projects/{projectName}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveProjectAccess(string username, string projectName, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new RemoveProjectAccessCommand(username, projectName), cancellationToken);

        if (!result.Success)
            return NotFound(new { error = result.Error });

        return NoContent();
    }
}
