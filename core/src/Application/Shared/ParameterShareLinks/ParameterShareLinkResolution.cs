using Nona.Application.Admin.Common;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Entities;
using Nona.Domain.Interfaces;

namespace Nona.Application.Shared.ParameterShareLinks;

internal static class ParameterShareLinkResolution
{
    public static async Task<ParameterShareLinkResolutionResult> ResolveAsync(
        IParameterShareLinkRepository shareLinkRepository,
        IDateTime dateTime,
        string token,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length != 16)
        {
            return ParameterShareLinkResolutionResult.Failed(
                "Share link not found.",
                ParameterShareLinkErrorCodes.Invalid);
        }

        var tokenHash = TokenHelper.Hash(token);
        var shareLink = await shareLinkRepository.GetByTokenHashAsync(tokenHash, cancellationToken);
        if (shareLink is null)
        {
            return ParameterShareLinkResolutionResult.Failed(
                "Share link not found.",
                ParameterShareLinkErrorCodes.Invalid);
        }

        if (shareLink.RevokedAt is not null)
        {
            return ParameterShareLinkResolutionResult.Failed(
                "This share link has been revoked.",
                ParameterShareLinkErrorCodes.Revoked);
        }

        if (shareLink.ExpiresAt <= dateTime.NowUtc)
        {
            return ParameterShareLinkResolutionResult.Failed(
                "This share link has expired.",
                ParameterShareLinkErrorCodes.Expired);
        }

        return ParameterShareLinkResolutionResult.Successful(shareLink);
    }
}

internal sealed record ParameterShareLinkResolutionResult(
    bool Success,
    ParameterShareLink? ShareLink,
    string? Error,
    string? ErrorCode)
{
    public static ParameterShareLinkResolutionResult Successful(ParameterShareLink shareLink)
        => new(true, shareLink, null, null);

    public static ParameterShareLinkResolutionResult Failed(string error, string errorCode)
        => new(false, null, error, errorCode);
}
