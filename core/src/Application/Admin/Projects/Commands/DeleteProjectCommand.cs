using MediatR;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Interfaces;

namespace Nona.Application.Admin.Projects.Commands;

public record DeleteProjectCommand(string ProjectId) : IRequest<DeleteProjectResult>;

public record DeleteProjectResult(bool Success, string? Error);

public class DeleteProjectCommandHandler(
    IProjectRepository projectRepository,
    IEnvironmentRepository environmentRepository,
    IConfigEntryRepository configEntryRepository,
    IProjectMemberRepository projectMemberRepository,
    ICurrentUserService currentUserService)
    : IRequestHandler<DeleteProjectCommand, DeleteProjectResult>
{
    public async Task<DeleteProjectResult> Handle(DeleteProjectCommand request, CancellationToken cancellationToken)
    {
        if (!await projectRepository.ExistsAsync(request.ProjectId, cancellationToken))
            return new DeleteProjectResult(false, "Project not found");

        // Only admin users can delete projects
        if (!currentUserService.IsAdmin)
            return new DeleteProjectResult(false, "Access denied. Only admin users can delete projects.");


        await DeleteConfigEntriesAsync(request, cancellationToken);
        await DeleteEnvironmentsAsync(request, cancellationToken);

        await projectMemberRepository.DeleteByProjectAsync(request.ProjectId, cancellationToken);

        await projectRepository.DeleteAsync(request.ProjectId, cancellationToken);

        return new DeleteProjectResult(true, null);
    }

    private async Task DeleteEnvironmentsAsync(DeleteProjectCommand request, CancellationToken cancellationToken)
    {
        var environments = await environmentRepository.ListByProjectAsync(request.ProjectId, cancellationToken);
        foreach (var env in environments)
        {
            await environmentRepository.DeleteAsync(request.ProjectId, env.Name, cancellationToken);
        }
    }

    private async Task DeleteConfigEntriesAsync(DeleteProjectCommand request, CancellationToken cancellationToken)
    {
        var configEntries = await configEntryRepository.ListByProjectAsync(request.ProjectId, cancellationToken);
        foreach (var entry in configEntries)
        {
            await configEntryRepository.DeleteAsync(entry.Project, entry.Environment, entry.Key, cancellationToken);
        }
    }
}
