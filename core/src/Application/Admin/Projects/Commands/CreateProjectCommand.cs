using MediatR;
using Microsoft.Extensions.Configuration;
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
    IEnvironmentRepository environmentRepository,
    IConfiguration configuration,
    IDateTime dateTime,
    IAuditLogService? auditLogService = null) : IRequestHandler<CreateProjectCommand, CreateProjectResult>
{
    public async Task<CreateProjectResult> Handle(CreateProjectCommand request, CancellationToken cancellationToken)
    {
        // Only admin users can create new projects
        if (!currentUserService.IsAdmin)
            return new CreateProjectResult(false, null, "Access denied. Only admin users can create projects.");

        if (await projectRepository.ExistsAsync(request.Name, cancellationToken))
            return new CreateProjectResult(false, null, "Project already exists");


        var now = dateTime.NowUtc;

        var baseSlug = GenerateSlug(request.Name);
        var finalSlug = baseSlug;
        if (!string.IsNullOrEmpty(baseSlug))
        {
            var existing = await projectRepository.ListAsync(cancellationToken);
            var used = existing.Select(p => p.UrlSlug).Where(s => !string.IsNullOrEmpty(s)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var attempt = 2;
            while (used.Contains(finalSlug))
            {
                finalSlug = $"{baseSlug}-{attempt}";
                attempt++;
            }
        }

        var project = new Project
        {
            Name = request.Name,
            UrlSlug = finalSlug,
            ServerApiKey = GenerateApiKey(),
            ClientApiKey = GenerateApiKey(),
            CreatedAt = now,
            UpdatedAt = now
        };

        await projectRepository.AddAsync(project, cancellationToken);

        var defaultEnvironments = configuration.GetSection("Defaults:Environment").Get<string[]>() ?? [];
        foreach (var env in defaultEnvironments)
        {
            await environmentRepository.AddAsync(new ProjectEnvironment
            {
                Name = env,
                Project = project.Name,
                CreatedAt = now,
                UpdatedAt = now
            }, cancellationToken);
        }

        if (auditLogService is not null)
        {
            await auditLogService.WriteAsync(
                "Created Project",
                project.Name,
                project: project.Name,
                cancellationToken: cancellationToken);
        }

        var dto = new ProjectDto(
            project.Id,
            project.Name,
            project.UrlSlug,
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

    private static string GenerateSlug(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var slug = name.Trim().ToLowerInvariant();
        // replace spaces and invalid chars with hyphens
        var sb = new System.Text.StringBuilder();
        foreach (var c in slug)
        {
            if (char.IsLetterOrDigit(c)) sb.Append(c);
            else if (char.IsWhiteSpace(c) || c == '-' || c == '_') sb.Append('-');
        }

        // collapse multiple hyphens
        var result = System.Text.RegularExpressions.Regex.Replace(sb.ToString(), "-+", "-").Trim('-');
        return result;
    }
}
