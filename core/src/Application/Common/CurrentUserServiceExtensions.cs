using Nona.Application.Common.Interfaces;

namespace Nona.Application.Common;

public static class CurrentUserServiceExtensions
{
    /// <summary>
    /// The username to attribute an action to (for audit trails and version
    /// history), falling back to "System" when there is no authenticated user.
    /// </summary>
    public static string ResolveActor(this ICurrentUserService? currentUserService)
        => string.IsNullOrWhiteSpace(currentUserService?.Username)
            ? "System"
            : currentUserService.Username!;
}
