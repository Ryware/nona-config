using Mediator;
using Nona.Application.Common.Interfaces;
using Nona.Application.Shared.ParameterShareLinks.DTOs;
using Nona.Domain.Entities;
using Nona.Domain.Interfaces;

namespace Nona.Application.Shared.ParameterShareLinks.Queries;

public record GetSharedParameterQuery(string Token) : IRequest<GetSharedParameterResult>;

public record GetSharedParameterResult(
    bool Success,
    SharedParameterDto? Parameter,
    string? Error,
    string? ErrorCode = null);

public class GetSharedParameterQueryHandler(
    IParameterShareLinkRepository shareLinkRepository,
    IConfigEntryRepository configEntryRepository,
    IDateTime dateTime,
    IAuditLogService? auditLogService = null)
    : IRequestHandler<GetSharedParameterQuery, GetSharedParameterResult>
{
    public async ValueTask<GetSharedParameterResult> Handle(
        GetSharedParameterQuery request,
        CancellationToken cancellationToken)
    {
        var resolution = await ParameterShareLinkResolution.ResolveAsync(
            shareLinkRepository,
            dateTime,
            request.Token,
            cancellationToken);

        if (!resolution.Success)
        {
            return new GetSharedParameterResult(false, null, resolution.Error, resolution.ErrorCode);
        }

        var shareLink = resolution.ShareLink!;
        var entry = await configEntryRepository.GetAsync(
            shareLink.Project,
            shareLink.Environment,
            shareLink.Key,
            cancellationToken);

        if (entry is null)
        {
            return new GetSharedParameterResult(
                false,
                null,
                "The shared parameter no longer exists.",
                ParameterShareLinkErrorCodes.Invalid);
        }

        if (auditLogService is not null)
        {
            await auditLogService.WriteAsAsync(
                ResolveSharedActor(shareLink),
                true,
                "Share Link Accessed",
                shareLink.Key,
                project: shareLink.Project,
                environment: shareLink.Environment,
                cancellationToken: cancellationToken);
        }

        return new GetSharedParameterResult(true, ToDto(entry, shareLink.CanEdit, shareLink.ExpiresAt), null);
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
