using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nona.Application.Admin.Projects.Commands;
using Nona.Application.Admin.Projects.DTOs;
using Nona.Application.Admin.Projects.Queries;

namespace Nona.WebApi.Controllers.Admin;

[ApiController]
[Authorize]
[Route("admin/projects")]
public class AdminProjectsController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(ProjectDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateProject([FromBody] CreateProjectRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new CreateProjectCommand(request.Name), cancellationToken);

        if (!result.Success)
        {
            return result.Error == "Project already exists"
                ? Conflict(new { error = result.Error })
                : BadRequest(new { error = result.Error });
        }

        return CreatedAtAction(nameof(ListProjects), result.Project);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ProjectDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListProjects(CancellationToken cancellationToken)
    {
        var projects = await mediator.Send(new ListProjectsQuery(), cancellationToken);
        return Ok(projects);
    }

    [HttpDelete("{projectId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteProject(string projectId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new DeleteProjectCommand(projectId), cancellationToken);

        if (!result.Success)
            return NotFound(new { error = result.Error });

        return NoContent();
    }

}
