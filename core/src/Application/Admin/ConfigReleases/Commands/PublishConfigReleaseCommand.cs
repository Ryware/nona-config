using Mediator;
using Nona.Application.Admin.ConfigReleases.DTOs;
using Nona.Application.Admin.ConfigReleases.Validators;
using Nona.Application.Admin.Projects;
using Nona.Application.Common;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Entities;
using Nona.Domain.Interfaces;

namespace Nona.Application.Admin.ConfigReleases.Commands;

public record PublishConfigReleaseRequest(
    string Version,
    bool MakeActive = false,
    IReadOnlyList<ConfigReleaseEntryDto>? Entries = null);

public record PublishConfigReleaseCommand(
    string ProjectId,
    string EnvironmentName,
    string Version,
    bool MakeActive,
    IReadOnlyList<ConfigReleaseEntryDto>? Entries = null)
    : IRequest<PublishConfigReleaseResult>;

public record PublishConfigReleaseResult(bool Success, ConfigReleaseDetailsDto? Release, string? Error);

public class PublishConfigReleaseCommandHandler(
    IProjectRepository projectRepository,
    IEnvironmentRepository environmentRepository,
    IConfigEntryRepository configEntryRepository,
    IConfigReleaseRepository configReleaseRepository,
    IProjectAccessService projectAccessService,
    IDateTime dateTime,
    ICurrentUserService? currentUserService = null,
    IAuditLogService? auditLogService = null)
    : IRequestHandler<PublishConfigReleaseCommand, PublishConfigReleaseResult>
{
    public async ValueTask<PublishConfigReleaseResult> Handle(PublishConfigReleaseCommand request, CancellationToken cancellationToken)
    {
        var project = await ProjectResolution.ResolveProjectAsync(projectRepository, request.ProjectId, cancellationToken);
        if (project is null)
            return new PublishConfigReleaseResult(false, null, "Project not found");

        var projectName = project.Name;
        if (!await projectAccessService.HasEditAccessAsync(projectName, cancellationToken))
            return new PublishConfigReleaseResult(false, null, "Access denied");

        var environment = await environmentRepository.GetAsync(projectName, request.EnvironmentName, cancellationToken);
        if (environment is null)
            return new PublishConfigReleaseResult(false, null, "Environment not found");

        if (!ConfigReleaseVersions.TryParseExact(request.Version, out var version))
            return new PublishConfigReleaseResult(false, null, "Version must use major.minor.patch format.");

        if (await configReleaseRepository.ExistsAsync(projectName, request.EnvironmentName, version.Normalized, cancellationToken))
            return new PublishConfigReleaseResult(false, null, "Release already exists");

        List<ConfigReleaseEntry> snapshotEntries;
        if (request.Entries is not null)
        {
            // Amend / publish-from-payload: snapshot exactly what the caller sent.
            // The working configuration is intentionally left untouched.
            var validation = PublishConfigReleaseEntryPayloadValidation.Validate(request.Entries);
            if (validation.Failures.Count > 0)
                return new PublishConfigReleaseResult(false, null, validation.Failures[0].ErrorMessage);

            snapshotEntries = validation.Entries
                .Select(entry => new ConfigReleaseEntry
                {
                    Project = projectName,
                    Environment = request.EnvironmentName,
                    ReleaseVersion = version.Normalized,
                    Key = entry.Key,
                    Value = entry.Value,
                    ContentType = entry.ContentType,
                    Scope = entry.Scope
                })
                .ToList();
        }
        else
        {
            // Create a version: snapshot the current working configuration.
            var entries = await configEntryRepository.ListAsync(projectName, request.EnvironmentName, cancellationToken);
            snapshotEntries = entries.Select(entry => new ConfigReleaseEntry
            {
                Project = projectName,
                Environment = request.EnvironmentName,
                ReleaseVersion = version.Normalized,
                Key = entry.Key,
                Value = entry.Value,
                ContentType = entry.ContentType,
                Scope = entry.Scope
            }).ToList();
        }

        var now = dateTime.NowUtc;
        var actor = currentUserService.ResolveActor();
        var release = new ConfigRelease
        {
            Project = projectName,
            Environment = request.EnvironmentName,
            Version = version.Normalized,
            Major = version.Major,
            Minor = version.Minor,
            Patch = version.Patch!.Value,
            Entries = snapshotEntries,
            EntryCount = snapshotEntries.Count,
            CreatedAt = now,
            Actor = actor
        };

        if (!await configReleaseRepository.AddAsync(release, cancellationToken))
            return new PublishConfigReleaseResult(false, null, "Release already exists");

        if (request.MakeActive)
        {
            environment.ActiveReleaseVersion = version.Normalized;
            environment.UpdatedAt = now;
            await environmentRepository.UpdateAsync(environment, cancellationToken);
        }

        if (auditLogService is not null)
        {
            await auditLogService.WriteAsync(
                "Published Config Release",
                version.Normalized,
                project: projectName,
                environment: request.EnvironmentName,
                cancellationToken: cancellationToken);
        }

        return new PublishConfigReleaseResult(
            true,
            ConfigReleaseMapping.ToDetailsDto(release, environment.ActiveReleaseVersion),
            null);
    }

}
