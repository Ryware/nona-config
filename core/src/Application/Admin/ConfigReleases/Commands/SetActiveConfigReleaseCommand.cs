using Mediator;
using Nona.Application.Admin.Environments.DTOs;
using Nona.Application.Admin.Projects;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Interfaces;

namespace Nona.Application.Admin.ConfigReleases.Commands;

public record SetActiveConfigReleaseRequest(string? Version);

public record SetActiveConfigReleaseCommand(string ProjectId, string EnvironmentName, string? Version)
    : IRequest<SetActiveConfigReleaseResult>;

public record SetActiveConfigReleaseResult(bool Success, EnvironmentDto? Environment, string? Error);

public class SetActiveConfigReleaseCommandHandler(
    IProjectRepository projectRepository,
    IEnvironmentRepository environmentRepository,
    IConfigReleaseRepository configReleaseRepository,
    IProjectAccessService projectAccessService,
    IDateTime dateTime,
    IAuditLogService? auditLogService = null)
    : IRequestHandler<SetActiveConfigReleaseCommand, SetActiveConfigReleaseResult>
{
    public async ValueTask<SetActiveConfigReleaseResult> Handle(SetActiveConfigReleaseCommand request, CancellationToken cancellationToken)
    {
        var project = await ProjectResolution.ResolveProjectAsync(projectRepository, request.ProjectId, cancellationToken);
        if (project is null)
            return new SetActiveConfigReleaseResult(false, null, "Project not found");

        var projectName = project.Name;
        if (!await projectAccessService.HasEditAccessAsync(projectName, cancellationToken))
            return new SetActiveConfigReleaseResult(false, null, "Access denied");

        var environment = await environmentRepository.GetAsync(projectName, request.EnvironmentName, cancellationToken);
        if (environment is null)
            return new SetActiveConfigReleaseResult(false, null, "Environment not found");

        string? activeReleaseVersion = null;
        if (!string.IsNullOrWhiteSpace(request.Version))
        {
            if (!ConfigReleaseVersions.TryParseExact(request.Version, out var version))
                return new SetActiveConfigReleaseResult(false, null, "Version must use major.minor.patch format.");

            if (!await configReleaseRepository.ExistsAsync(projectName, request.EnvironmentName, version.Normalized, cancellationToken))
                return new SetActiveConfigReleaseResult(false, null, "Release not found");

            activeReleaseVersion = version.Normalized;
        }

        environment.ActiveReleaseVersion = activeReleaseVersion;
        environment.UpdatedAt = dateTime.NowUtc;
        await environmentRepository.UpdateAsync(environment, cancellationToken);

        if (auditLogService is not null)
        {
            await auditLogService.WriteAsync(
                activeReleaseVersion is null ? "Cleared Active Config Release" : "Set Active Config Release",
                activeReleaseVersion ?? request.EnvironmentName,
                project: projectName,
                environment: request.EnvironmentName,
                cancellationToken: cancellationToken);
        }

        return new SetActiveConfigReleaseResult(
            true,
            new EnvironmentDto(
                environment.Name,
                environment.Project,
                environment.ActiveReleaseVersion,
                environment.CreatedAt,
                environment.UpdatedAt),
            null);
    }
}
