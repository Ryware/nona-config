using Nona.Domain.Entities;
using Nona.Domain.Enums;

namespace Nona.Application.Common;

public static class EnumExtensions
{
    public static string ToApiString(this KeyScope scope) => scope switch
    {
        KeyScope.Backend => "server",
        KeyScope.Frontend => "client",
        KeyScope.All => "all",
        _ => scope.ToString().ToLowerInvariant()
    };

    public static string ToApiString(this UserRole role) => role switch
    {
        UserRole.User => "user",
        UserRole.Admin => "admin",
        _ => role.ToString().ToLowerInvariant()
    };

    public static string ToApiString(this ProjectRole role) => role switch
    {
        ProjectRole.User => "user",
        ProjectRole.Admin => "admin",
        _ => role.ToString().ToLowerInvariant()
    };
}
