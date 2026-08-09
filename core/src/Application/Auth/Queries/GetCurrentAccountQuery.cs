using Mediator;
using Nona.Application.Auth.DTOs;
using Nona.Application.Common;
using Nona.Application.Common.Interfaces;

namespace Nona.Application.Auth.Queries;

public record GetCurrentAccountQuery : IRequest<GetCurrentAccountResult>;

public record GetCurrentAccountResult(
    bool Success,
    AccountDetailsResponse? Account,
    string? Error);

public sealed class GetCurrentAccountQueryHandler(IUserAuthorizationService userAuthorizationService)
    : IRequestHandler<GetCurrentAccountQuery, GetCurrentAccountResult>
{
    public async ValueTask<GetCurrentAccountResult> Handle(
        GetCurrentAccountQuery request,
        CancellationToken cancellationToken)
    {
        var user = await userAuthorizationService.GetCurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return new GetCurrentAccountResult(false, null, "User not found");
        }

        return new GetCurrentAccountResult(
            true,
            new AccountDetailsResponse(
                user.Email,
                user.Name,
                user.Role.ToApiString(),
                !string.IsNullOrEmpty(user.PasswordHash)),
            null);
    }
}
