using Mediator;
using Nona.Application.Admin.Projects;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Interfaces;

namespace Nona.Application.Admin.ConfigReleases.Commands;

public record DeleteConfigReleaseCommand(string ProjectId, string EnvironmentName, string Version)
    : IRequest<DeleteConfigReleaseResult>;

public record DeleteConfigReleaseResult(bool Success, string? Error);

public class DeleteConfigReleaseCommandHandler(
    IProjectRepository projectRepository,
    IEnvironmentRepository environmentRepository,
    IConfigReleaseRepository configReleaseRepository,
    IProjectAccessService projectAccessService,
    IAuditLogService? auditLogService = null)
    : IRequestHandler<DeleteConfigReleaseCommand, DeleteConfigReleaseResult>
{
    public async ValueTask<DeleteConfigReleaseResult> Handle(
        DeleteConfigReleaseCommand request,
        CancellationToken cancellationToken)
    {
        var project = await ProjectResolution.ResolveProjectAsync(projectRepository, request.ProjectId, cancellationToken);
        if (project is null)
            return new DeleteConfigReleaseResult(false, "Project not found");

        var projectName = project.Name;
        if (!await projectAccessService.HasEditAccessAsync(projectName, cancellationToken))
            return new DeleteConfigReleaseResult(false, "Access denied");

        var environment = await environmentRepository.GetAsync(projectName, request.EnvironmentName, cancellationToken);
        if (environment is null)
            return new DeleteConfigReleaseResult(false, "Environment not found");

        if (!ConfigReleaseVersions.TryParseExact(request.Version, out var version))
            return new DeleteConfigReleaseResult(false, "Version must use major.minor.patch format.");

        if (string.Equals(environment.ActiveReleaseVersion, version.Normalized, StringComparison.OrdinalIgnoreCase))
            return new DeleteConfigReleaseResult(false, "Active release cannot be deleted");

        if (!await configReleaseRepository.DeleteAsync(
                projectName,
                request.EnvironmentName,
                version.Normalized,
                cancellationToken))
        {
            return new DeleteConfigReleaseResult(false, "Release not found");
        }

        if (auditLogService is not null)
        {
            await auditLogService.WriteAsync(
                "Deleted Config Release",
                version.Normalized,
                project: projectName,
                environment: request.EnvironmentName,
                cancellationToken: cancellationToken);
        }

        return new DeleteConfigReleaseResult(true, null);
    }
}
