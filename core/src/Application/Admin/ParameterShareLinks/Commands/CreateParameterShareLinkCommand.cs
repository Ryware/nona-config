using Mediator;
using Nona.Application.Admin.Common;
using Nona.Application.Admin.ParameterShareLinks.DTOs;
using Nona.Application.Admin.Projects;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Entities;
using Nona.Domain.Interfaces;

namespace Nona.Application.Admin.ParameterShareLinks.Commands;

public record CreateParameterShareLinkRequest(string? Expiration = null, bool CanEdit = true);

public record CreateParameterShareLinkCommand(
    string ProjectId,
    string EnvironmentName,
    string Key,
    string? Expiration,
    bool CanEdit) : IRequest<CreateParameterShareLinkResult>;

public record CreateParameterShareLinkResult(
    bool Success,
    CreatedParameterShareLinkDto? ShareLink,
    string? Error);

public class CreateParameterShareLinkCommandHandler(
    IProjectRepository projectRepository,
    IEnvironmentRepository environmentRepository,
    IConfigEntryRepository configEntryRepository,
    IParameterShareLinkRepository shareLinkRepository,
    IProjectAccessService projectAccessService,
    IDateTime dateTime,
    ICurrentUserService? currentUserService = null,
    IAuditLogService? auditLogService = null)
    : IRequestHandler<CreateParameterShareLinkCommand, CreateParameterShareLinkResult>
{
    private const int TokenLength = 16;

    public async ValueTask<CreateParameterShareLinkResult> Handle(
        CreateParameterShareLinkCommand request,
        CancellationToken cancellationToken)
    {
        var project = await ProjectResolution.ResolveProjectAsync(projectRepository, request.ProjectId, cancellationToken);
        if (project is null)
            return new CreateParameterShareLinkResult(false, null, "Project not found");

        var projectName = project.Name;
        if (!await projectAccessService.HasEditAccessAsync(projectName, cancellationToken))
            return new CreateParameterShareLinkResult(false, null, "Access denied");

        if (!await environmentRepository.ExistsAsync(projectName, request.EnvironmentName, cancellationToken))
            return new CreateParameterShareLinkResult(false, null, "Environment not found");

        if (!await configEntryRepository.ExistsAsync(projectName, request.EnvironmentName, request.Key, cancellationToken))
            return new CreateParameterShareLinkResult(false, null, "Config entry not found");

        if (!TryResolveExpiration(request.Expiration, dateTime.NowUtc, out var expiresAt, out var expirationError))
            return new CreateParameterShareLinkResult(false, null, expirationError);

        var token = await GenerateUniqueTokenAsync(shareLinkRepository, cancellationToken);
        var now = dateTime.NowUtc;
        var shareLink = new ParameterShareLink
        {
            TokenHash = TokenHelper.Hash(token),
            Project = projectName,
            Environment = request.EnvironmentName,
            Key = request.Key,
            CanEdit = request.CanEdit,
            CreatedBy = ResolveCreator(),
            CreatedAt = now,
            ExpiresAt = expiresAt
        };

        await shareLinkRepository.AddAsync(shareLink, cancellationToken);

        if (auditLogService is not null)
        {
            await auditLogService.WriteAsync(
                "Share Link Created",
                request.Key,
                project: projectName,
                environment: request.EnvironmentName,
                cancellationToken: cancellationToken);
        }

        return new CreateParameterShareLinkResult(
            true,
            ParameterShareLinkMapping.ToCreatedDto(shareLink, token),
            null);
    }

    private static bool TryResolveExpiration(
        string? value,
        DateTime now,
        out DateTime expiresAt,
        out string? error)
    {
        error = null;
        var normalized = string.IsNullOrWhiteSpace(value)
            ? "1h"
            : value.Trim().ToLowerInvariant();

        expiresAt = normalized switch
        {
            "1h" or "1-hour" or "1 hour" => now.AddHours(1),
            "1d" or "1-day" or "1 day" => now.AddDays(1),
            "3d" or "3-days" or "3 days" => now.AddDays(3),
            "30d" or "30-days" or "30 days" => now.AddDays(30),
            "12m" or "12-months" or "12 months" => now.AddMonths(12),
            _ => now
        };

        if (expiresAt != now)
            return true;

        error = "Expiration must be one of: 1h, 1d, 3d, 30d, 12m.";
        return false;
    }

    private static async Task<string> GenerateUniqueTokenAsync(
        IParameterShareLinkRepository shareLinkRepository,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var token = TokenHelper.GenerateUrlSafe(TokenLength);
            var tokenHash = TokenHelper.Hash(token);
            if (await shareLinkRepository.GetByTokenHashAsync(tokenHash, cancellationToken) is null)
                return token;
        }

        throw new InvalidOperationException("Unable to generate a unique share token.");
    }

    private string ResolveCreator()
    {
        return string.IsNullOrWhiteSpace(currentUserService?.Username)
            ? "System"
            : currentUserService.Username!;
    }
}
