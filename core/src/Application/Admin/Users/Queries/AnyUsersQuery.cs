using Mediator;
using Nona.Domain.Interfaces;

namespace Nona.Application.Admin.Users.Queries;

public record AnyUsersQuery : IRequest<bool>;

internal class AnyUsersQueryHandler(IUserRepository userRepository) : IRequestHandler<AnyUsersQuery, bool>
{
    public async ValueTask<bool> Handle(AnyUsersQuery request, CancellationToken cancellationToken)
    {
        var usersExist = await userRepository.ExistsAnyAsync(cancellationToken);

        return !usersExist;
    }
}
