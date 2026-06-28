using Mediator;
using Nona.Application.Admin.Common;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Interfaces;

namespace Nona.Application.Auth.Commands;

public record RequestPasswordResetCommand(string Email) : IRequest;

internal class RequestPasswordResetCommandHandler(
    IUserRepository userRepository,
    IDateTime dateTime)
    : IRequestHandler<RequestPasswordResetCommand>
{
    public async ValueTask<Unit> Handle(RequestPasswordResetCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetAsync(request.Email, cancellationToken);
        if (user is null)
        {
            return Unit.Value;
        }

        user.PasswordResetToken = TokenHelper.Hash(TokenHelper.Generate());
        user.UpdatedAt = dateTime.NowUtc;

        await userRepository.UpdateAsync(user, cancellationToken);
        return Unit.Value;
    }
}
