using Mediator;
using Nona.Application.Admin.Common;
using Nona.Application.Auth.DTOs;
using Nona.Domain.Interfaces;

namespace Nona.Application.Auth.Queries;

public record GetInvitationQuery(string Token) : IRequest<GetInvitationResult>;

public record GetInvitationResult(bool Success, InvitationDetailsResponse? Invitation, string? Error, string? ErrorCode = null);

public class GetInvitationQueryHandler(IUserRepository userRepository) : IRequestHandler<GetInvitationQuery, GetInvitationResult>
{
    public async ValueTask<GetInvitationResult> Handle(GetInvitationQuery request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByInviteTokenHashAsync(TokenHelper.Hash(request.Token), cancellationToken);
        if (user is null)
        {
            return new GetInvitationResult(false, null, "Invitation is invalid or has already been used.", AuthErrorCodes.InvitationInvalidOrUsed);
        }

        return new GetInvitationResult(true, new InvitationDetailsResponse(user.Email, user.Name), null);
    }
}
