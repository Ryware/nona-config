using MediatR;
using Nona.Application.Admin.Projects.DTOs;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Entities;
using Nona.Domain.Interfaces;
using System.Security.Cryptography;

namespace Nona.Application.Admin.Projects.Commands;

public record CreateProjectRequest(string Name);
public record CreateProjectCommand(string Name) : IRequest<CreateProjectResult>;

public record CreateProjectResult(bool Success, ProjectDto? Project, string? Error);

public class CreateProjectCommandHandler(
    IProjectRepository projectRepository,
    ICurrentUserService currentUserService,
    IDateTime dateTime) : IRequestHandler<CreateProjectCommand, CreateProjectResult>
{
    public async Task<CreateProjectResult> Handle(CreateProjectCommand request, CancellationToken cancellationToken)
    {
        // Only admin users can create new projects
        if (!currentUserService.IsAdmin)
            return new CreateProjectResult(false, null, "Access denied. Only admin users can create projects.");

        if (await projectRepository.ExistsAsync(request.Name, cancellationToken))
            return new CreateProjectResult(false, null, "Project already exists");


        var now = dateTime.NowUtc;
        var project = new Project
        {
            Name = request.Name,
            ServerApiKey = GenerateApiKey(),
            ClientApiKey = GenerateApiKey(),
            CreatedAt = now,
            UpdatedAt = now
        };

        await projectRepository.AddAsync(project, cancellationToken);

        var dto = new ProjectDto(
            project.Name,
            project.ServerApiKey,
            project.ClientApiKey,
            project.Environments,
            project.CreatedAt,
            project.UpdatedAt);

        return new CreateProjectResult(true, dto, null);
    }

    private static string GenerateApiKey()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    }
}
