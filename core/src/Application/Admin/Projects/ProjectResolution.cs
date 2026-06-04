using Nona.Domain.Entities;
using Nona.Domain.Interfaces;

namespace Nona.Application.Admin.Projects;

internal static class ProjectResolution
{
    public static async Task<Project?> ResolveProjectAsync(
        IProjectRepository projectRepository,
        string idOrNameOrSlug,
        CancellationToken cancellationToken)
    {
        var project = await projectRepository.GetByNameAsync(idOrNameOrSlug, cancellationToken);
        if (project is not null)
            return project;

        var projects = await projectRepository.ListAsync(cancellationToken);

        if (long.TryParse(idOrNameOrSlug, out var numericId))
        {
            var byId = projects.FirstOrDefault(p => p.Id == numericId);
            if (byId is not null)
                return byId;
        }

        return projects.FirstOrDefault(p =>
            string.Equals(p.Name, idOrNameOrSlug, StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrEmpty(p.UrlSlug) &&
             string.Equals(p.UrlSlug, idOrNameOrSlug, StringComparison.OrdinalIgnoreCase)));
    }
}
