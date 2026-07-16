using Mediator;
using Nona.Application.Admin.ConfigEntries;
using Nona.Application.Admin.ConfigEntries.DTOs;
using Nona.Application.Admin.Projects;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Entities;
using Nona.Domain.Interfaces;

namespace Nona.Application.Admin.ConfigReleases.Commands;

public record CreateConfigReleaseDraftCommand(string ProjectId, string EnvironmentName, string Version)
    : IRequest<CreateConfigReleaseDraftResult>;

public record CreateConfigReleaseDraftResult(bool Success, IReadOnlyList<ConfigEntryDto>? ConfigEntries, string? Error);

public class CreateConfigReleaseDraftCommandHandler(
    IProjectRepository projectRepository,
    IEnvironmentRepository environmentRepository,
    IConfigEntryRepository configEntryRepository,
    IConfigReleaseRepository configReleaseRepository,
    IProjectAccessService projectAccessService,
    IDateTime dateTime,
    ICurrentUserService? currentUserService = null,
    IAuditLogService? auditLogService = null)
    : IRequestHandler<CreateConfigReleaseDraftCommand, CreateConfigReleaseDraftResult>
{
    public async ValueTask<CreateConfigReleaseDraftResult> Handle(CreateConfigReleaseDraftCommand request, CancellationToken cancellationToken)
    {
        var project = await ProjectResolution.ResolveProjectAsync(projectRepository, request.ProjectId, cancellationToken);
        if (project is null)
            return new CreateConfigReleaseDraftResult(false, null, "Project not found");

        var projectName = project.Name;
        if (!await projectAccessService.HasEditAccessAsync(projectName, cancellationToken))
            return new CreateConfigReleaseDraftResult(false, null, "Access denied");

        var environment = await environmentRepository.GetAsync(projectName, request.EnvironmentName, cancellationToken);
        if (environment is null)
            return new CreateConfigReleaseDraftResult(false, null, "Environment not found");

        if (!ConfigReleaseVersions.TryParseExact(request.Version, out var version))
            return new CreateConfigReleaseDraftResult(false, null, "Version must use major.minor.patch format.");

        var release = await configReleaseRepository.GetAsync(projectName, request.EnvironmentName, version.Normalized, cancellationToken);
        if (release is null)
            return new CreateConfigReleaseDraftResult(false, null, "Release not found");

        var now = dateTime.NowUtc;
        var actor = ResolveActor();
        var existingEntries = await configEntryRepository.ListAsync(projectName, request.EnvironmentName, cancellationToken);
        var existingByKey = existingEntries.ToDictionary(entry => entry.Key, StringComparer.OrdinalIgnoreCase);
        var releaseKeys = release.Entries.Select(entry => entry.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var replacementEntries = new List<ConfigEntry>();

        foreach (var releaseEntry in release.Entries)
        {
            existingByKey.TryGetValue(releaseEntry.Key, out var existingEntry);
            replacementEntries.Add(new ConfigEntry
            {
                Project = projectName,
                Environment = request.EnvironmentName,
                Key = releaseEntry.Key,
                Value = releaseEntry.Value,
                ContentType = releaseEntry.ContentType,
                Scope = releaseEntry.Scope,
                CreatedAt = existingEntry?.CreatedAt ?? now,
                UpdatedAt = now
            });
        }

        var deletedKeys = existingEntries
            .Where(entry => !releaseKeys.Contains(entry.Key))
            .Select(entry => entry.Key)
            .ToList();
        await configEntryRepository.ReplaceEnvironmentAsync(
            projectName,
            request.EnvironmentName,
            replacementEntries,
            deletedKeys,
            actor,
            cancellationToken);

        var savedEntries = await configEntryRepository.ListAsync(projectName, request.EnvironmentName, cancellationToken);

        environment.UpdatedAt = now;
        await environmentRepository.UpdateAsync(environment, cancellationToken);

        if (auditLogService is not null)
        {
            await auditLogService.WriteAsync(
                "Created Config Release Draft",
                version.Normalized,
                project: projectName,
                environment: request.EnvironmentName,
                cancellationToken: cancellationToken);
        }

        return new CreateConfigReleaseDraftResult(
            true,
            savedEntries.Select(ConfigEntryMapping.ToDto).ToList(),
            null);
    }

    private string ResolveActor()
    {
        return string.IsNullOrWhiteSpace(currentUserService?.Username)
            ? "System"
            : currentUserService.Username!;
    }
}
