using Nona.Domain.Entities;

namespace Nona.Application.Admin.ParameterShareLinks.DTOs;

public record ParameterShareLinkDto(
    long Id,
    string Token,
    string Project,
    string Environment,
    string Key,
    bool CanEdit,
    string CreatedBy,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    DateTime? RevokedAt);

public record CreatedParameterShareLinkDto(
    long Id,
    string Token,
    string Project,
    string Environment,
    string Key,
    bool CanEdit,
    string CreatedBy,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    DateTime? RevokedAt);

internal static class ParameterShareLinkMapping
{
    public static ParameterShareLinkDto ToDto(ParameterShareLink shareLink)
    {
        return new ParameterShareLinkDto(
            shareLink.Id,
            shareLink.Token,
            shareLink.Project,
            shareLink.Environment,
            shareLink.Key,
            shareLink.CanEdit,
            shareLink.CreatedBy,
            shareLink.CreatedAt,
            shareLink.ExpiresAt,
            shareLink.RevokedAt);
    }

    public static CreatedParameterShareLinkDto ToCreatedDto(ParameterShareLink shareLink, string token)
    {
        return new CreatedParameterShareLinkDto(
            shareLink.Id,
            token,
            shareLink.Project,
            shareLink.Environment,
            shareLink.Key,
            shareLink.CanEdit,
            shareLink.CreatedBy,
            shareLink.CreatedAt,
            shareLink.ExpiresAt,
            shareLink.RevokedAt);
    }
}
