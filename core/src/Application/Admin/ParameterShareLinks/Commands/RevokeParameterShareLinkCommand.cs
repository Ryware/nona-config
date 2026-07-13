using Mediator;
using Nona.Application.Admin.Projects;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Interfaces;

namespace Nona.Application.Admin.ParameterShareLinks.Commands;

public record RevokeParameterShareLinkCommand(
    string ProjectId,
    string EnvironmentName,
    string Key,
    long ShareLinkId) : IRequest<RevokeParameterShareLinkResult>;

public record RevokeParameterShareLinkResult(bool Success, string? Error);

public class RevokeParameterShareLinkCommandHandler(
    IProjectRepository projectRepository,
    IParameterShareLinkRepository shareLinkRepository,
    IProjectAccessService projectAccessService,
    IDateTime dateTime,
    IAuditLogService? auditLogService = null)
    : IRequestHandler<RevokeParameterShareLinkCommand, RevokeParameterShareLinkResult>
{
    public async ValueTask<RevokeParameterShareLinkResult> Handle(
        RevokeParameterShareLinkCommand request,
        CancellationToken cancellationToken)
    {
        var project = await ProjectResolution.ResolveProjectAsync(projectRepository, request.ProjectId, cancellationToken);
        if (project is null)
            return new RevokeParameterShareLinkResult(false, "Project not found");

        var projectName = project.Name;
        if (!await projectAccessService.HasEditAccessAsync(projectName, cancellationToken))
            return new RevokeParameterShareLinkResult(false, "Access denied");

        var shareLink = await shareLinkRepository.GetByIdAsync(request.ShareLinkId, cancellationToken);
        if (shareLink is null || !MatchesScope(shareLink, projectName, request.EnvironmentName, request.Key))
            return new RevokeParameterShareLinkResult(false, "Share link not found");

        if (shareLink.RevokedAt is null)
        {
            await shareLinkRepository.RevokeAsync(request.ShareLinkId, dateTime.NowUtc, cancellationToken);

            if (auditLogService is not null)
            {
                await auditLogService.WriteAsync(
                    "Share Link Revoked",
                    request.Key,
                    project: projectName,
                    environment: request.EnvironmentName,
                    cancellationToken: cancellationToken);
            }
        }

        return new RevokeParameterShareLinkResult(true, null);
    }

    private static bool MatchesScope(
        Domain.Entities.ParameterShareLink shareLink,
        string projectName,
        string environmentName,
        string key)
    {
        return string.Equals(shareLink.Project, projectName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(shareLink.Environment, environmentName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(shareLink.Key, key, StringComparison.OrdinalIgnoreCase);
    }
}
