using MediatR;
using Nona.Application.Admin.ApiKeys.DTOs;
using Nona.Application.Admin.Projects;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Interfaces;

namespace Nona.Application.Admin.ApiKeys.Queries;

public record ListApiKeysQuery(string ProjectId) : IRequest<ListApiKeysResult>;

public record ListApiKeysResult(bool Success, IReadOnlyList<ApiKeyDto> ApiKeys, string? Error);

public class ListApiKeysQueryHandler(
    IProjectRepository projectRepository,
    IApiKeyRepository apiKeyRepository,
    IProjectAccessService projectAccessService) : IRequestHandler<ListApiKeysQuery, ListApiKeysResult>
{
    public async Task<ListApiKeysResult> Handle(ListApiKeysQuery request, CancellationToken cancellationToken)
    {
        var project = await ProjectResolution.ResolveProjectAsync(projectRepository, request.ProjectId, cancellationToken);
        if (project is null)
            return new ListApiKeysResult(false, [], "Project not found");

        if (!await projectAccessService.HasAdminAccessAsync(project.Name, cancellationToken))
            return new ListApiKeysResult(false, [], "Access denied");

        var apiKeys = await apiKeyRepository.ListByProjectAsync(project.Name, cancellationToken);
        return new ListApiKeysResult(true, apiKeys.Select(k => k.ToDto()).ToList(), null);
    }
}
