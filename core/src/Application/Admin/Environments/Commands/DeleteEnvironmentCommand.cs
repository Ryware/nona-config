using MediatR;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Interfaces;

namespace Nona.Application.Admin.Environments.Commands;

public record DeleteEnvironmentCommand(string ProjectId, string EnvironmentId) : IRequest<DeleteEnvironmentResult>;

public record DeleteEnvironmentResult(bool Success, string? Error);

public class DeleteEnvironmentCommandHandler(
    IProjectRepository projectRepository,
    IEnvironmentRepository environmentRepository,
    IConfigEntryRepository configEntryRepository,
    IProjectAccessService projectAccessService) : IRequestHandler<DeleteEnvironmentCommand, DeleteEnvironmentResult>
{
    public async Task<DeleteEnvironmentResult> Handle(DeleteEnvironmentCommand request, CancellationToken cancellationToken)
    {
        if (!await projectRepository.ExistsAsync(request.ProjectId, cancellationToken))
            return new DeleteEnvironmentResult(false, "Project not found");

        if (!await projectAccessService.HasAdminAccessAsync(request.ProjectId, cancellationToken))
            return new DeleteEnvironmentResult(false, "Access denied");

        if (!await environmentRepository.ExistsAsync(request.ProjectId, request.EnvironmentId, cancellationToken))
            return new DeleteEnvironmentResult(false, "Environment not found");

        await DeleteEnvironmentConfigEntries(request, cancellationToken);

        await environmentRepository.DeleteAsync(request.ProjectId, request.EnvironmentId, cancellationToken);

        return new DeleteEnvironmentResult(true, null);
    }

    private async Task DeleteEnvironmentConfigEntries(DeleteEnvironmentCommand request, CancellationToken cancellationToken)
    {
        var configEntries = await configEntryRepository.ListAsync(request.ProjectId, request.EnvironmentId, cancellationToken);
        var keys = configEntries.Select(e => e.Key).ToList();
        if (keys.Count > 0)
        {
            await configEntryRepository.DeleteManyAsync(request.ProjectId, request.EnvironmentId, keys, cancellationToken);
        }
    }
}
