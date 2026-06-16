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
    [ProducesResponseType(typeof(CreateUserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new CreateUserCommand(request.Name, request.Email, request.Role, request.Scope), cancellationToken);

        if (!result.Success)
        {
            return result.Error == "User already exists"
                ? Conflict(new { error = result.Error })
                : BadRequest(new { error = result.Error });
        }

        return CreatedAtAction(nameof(GetUser), new { id = result.Response!.User.Id }, result.Response);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<UserDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListUsers(CancellationToken cancellationToken)
    {
        var users = await mediator.Send(new ListUsersQuery(), cancellationToken);

        return Ok(users);
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUser(long id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetUserQuery(id), cancellationToken);

        if (!result.Success)
            return NotFound(new { error = result.Error });

        return Ok(result.User);
    }

    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateUser(long id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new UpdateUserCommand(id, request.Name, request.Role, request.Scope), cancellationToken);

        if (!result.Success)
        {
            return result.Error == "User not found"
                ? NotFound(new { error = result.Error })
                : BadRequest(new { error = result.Error });
        }

        return Ok(result.User);
    }

    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteUser(long id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new DeleteUserCommand(id), cancellationToken);

        if (!result.Success)
        {
            return result.Error == "User not found"
                ? NotFound(new { error = result.Error })
                : BadRequest(new { error = result.Error });
        }

        return NoContent();
    }

    [HttpGet("{id:long}/projects")]
    [ProducesResponseType(typeof(IReadOnlyList<ProjectAccessDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserProjects(long id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetUserProjectsQuery(id), cancellationToken);

        if (!result.Success)
            return NotFound(new { error = result.Error });

        return Ok(result.Projects);
    }

    [HttpPut("{id:long}/projects/{projectName}")]
    [ProducesResponseType(typeof(ProjectAccessDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetProjectAccess(long id, string projectName, [FromBody] ProjectAccessRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new SetProjectAccessCommand(id, projectName, request.Role), cancellationToken);

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

    [HttpDelete("{id:long}/projects/{projectName}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveProjectAccess(long id, string projectName, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new RemoveProjectAccessCommand(id, projectName), cancellationToken);

        if (!result.Success)
            return NotFound(new { error = result.Error });

        return NoContent();
    }
}
