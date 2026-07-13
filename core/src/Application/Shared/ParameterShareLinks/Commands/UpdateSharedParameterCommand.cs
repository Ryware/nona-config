using Mediator;
using Nona.Application.Common;
using Nona.Application.Common.Interfaces;
using Nona.Application.Shared.ParameterShareLinks.DTOs;
using Nona.Domain.Entities;
using Nona.Domain.Interfaces;

namespace Nona.Application.Shared.ParameterShareLinks.Commands;

public record UpdateSharedParameterRequest(string Value);

public record UpdateSharedParameterCommand(string Token, string Value) : IRequest<UpdateSharedParameterResult>;

public record UpdateSharedParameterResult(
    bool Success,
    SharedParameterDto? Parameter,
    string? Error,
    string? ErrorCode = null);

public class UpdateSharedParameterCommandHandler(
    IParameterShareLinkRepository shareLinkRepository,
    IConfigEntryRepository configEntryRepository,
    IDateTime dateTime,
    IAuditLogService? auditLogService = null)
    : IRequestHandler<UpdateSharedParameterCommand, UpdateSharedParameterResult>
{
    public async ValueTask<UpdateSharedParameterResult> Handle(
        UpdateSharedParameterCommand request,
        CancellationToken cancellationToken)
    {
        var resolution = await ParameterShareLinkResolution.ResolveAsync(
            shareLinkRepository,
            dateTime,
            request.Token,
            cancellationToken);

        if (!resolution.Success)
        {
            return new UpdateSharedParameterResult(false, null, resolution.Error, resolution.ErrorCode);
        }

        var shareLink = resolution.ShareLink!;
        if (!shareLink.CanEdit)
        {
            return new UpdateSharedParameterResult(
                false,
                null,
                "This share link is view-only.",
                ParameterShareLinkErrorCodes.ViewOnly);
        }

        var existingEntry = await configEntryRepository.GetAsync(
            shareLink.Project,
            shareLink.Environment,
            shareLink.Key,
            cancellationToken);

        if (existingEntry is null)
        {
            return new UpdateSharedParameterResult(
                false,
                null,
                "The shared parameter no longer exists.",
                ParameterShareLinkErrorCodes.Invalid);
        }

        var contentType = ConfigEntryContentTypes.Normalize(existingEntry.ContentType)
            ?? ConfigEntryContentTypes.Infer(existingEntry.Value);

        if (!ConfigEntryContentTypes.IsValidValue(request.Value, contentType, out var validationError))
        {
            return new UpdateSharedParameterResult(false, null, validationError, null);
        }

        var now = dateTime.NowUtc;
        var updatedEntry = new ConfigEntry
        {
            Project = existingEntry.Project,
            Environment = existingEntry.Environment,
            Key = existingEntry.Key,
            Value = request.Value,
            ContentType = contentType,
            Scope = existingEntry.Scope,
            CreatedAt = existingEntry.CreatedAt,
            UpdatedAt = now
        };

        var savedEntry = await configEntryRepository.AddVersionAsync(
            updatedEntry,
            ResolveSharedActor(shareLink),
            cancellationToken);

        if (savedEntry is null)
        {
            return new UpdateSharedParameterResult(false, null, "Parameter could not be updated.", null);
        }

        if (auditLogService is not null)
        {
            await auditLogService.WriteAsAsync(
                ResolveSharedActor(shareLink),
                true,
                "Parameter Updated Via Shared Link",
                shareLink.Key,
                project: shareLink.Project,
                environment: shareLink.Environment,
                cancellationToken: cancellationToken);
        }

        return new UpdateSharedParameterResult(
            true,
            ToDto(savedEntry, shareLink.CanEdit, shareLink.ExpiresAt),
            null);
    }

    private static string ResolveSharedActor(ParameterShareLink shareLink)
        => $"Shared link #{shareLink.Id}";

    private static SharedParameterDto ToDto(ConfigEntry entry, bool canEdit, DateTime expiresAt)
    {
        return new SharedParameterDto(
            entry.Environment,
            entry.Key,
            entry.Value,
            entry.ContentType,
            canEdit,
            expiresAt);
    }
}
