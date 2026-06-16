using MediatR;
using Nona.Application.Admin.Environments.DTOs;
using Nona.Application.Admin.Projects;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Entities;
using Nona.Domain.Interfaces;

namespace Nona.Application.Admin.Environments.Commands;

public record CreateEnvironmentRequest(string Name);
public record CreateEnvironmentCommand(string ProjectId, string Name) : IRequest<CreateEnvironmentResult>;

public record CreateEnvironmentResult(bool Success, EnvironmentDto? Environment, string? Error);

public class CreateEnvironmentCommandHandler(
    IProjectRepository projectRepository,
    IEnvironmentRepository environmentRepository,
    IProjectAccessService projectAccessService,
    IDateTime dateTime,
    IAuditLogService? auditLogService = null) : IRequestHandler<CreateEnvironmentCommand, CreateEnvironmentResult>
{
    public async Task<CreateEnvironmentResult> Handle(CreateEnvironmentCommand request, CancellationToken cancellationToken)
    {
        var project = await ProjectResolution.ResolveProjectAsync(projectRepository, request.ProjectId, cancellationToken);
        if (project is null)
            return new CreateEnvironmentResult(false, null, "Project not found");

        var projectName = project.Name;
        if (!await projectAccessService.HasEditAccessAsync(projectName, cancellationToken))
            return new CreateEnvironmentResult(false, null, "Access denied");

        if (await environmentRepository.ExistsAsync(projectName, request.Name, cancellationToken))
            return new CreateEnvironmentResult(false, null, "Environment already exists");


        var now = dateTime.NowUtc;
        var environment = new ProjectEnvironment
        {
            Name = request.Name,
            Project = projectName,
            CreatedAt = now,
            UpdatedAt = now
        };

        await environmentRepository.AddAsync(environment, cancellationToken);

        if (auditLogService is not null)
        {
            await auditLogService.WriteAsync(
                "Created Environment",
                environment.Name,
                project: environment.Project,
                environment: environment.Name,
                cancellationToken: cancellationToken);
        }

        var dto = new EnvironmentDto(
            environment.Name,
            environment.Project,
            environment.CreatedAt,
            environment.UpdatedAt);

        return new CreateEnvironmentResult(true, dto, null);
    }
}
