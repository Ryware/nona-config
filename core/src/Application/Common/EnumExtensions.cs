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
        UserRole.Viewer => "viwer",
        UserRole.Editor => "editor",
        _ => role.ToString().ToLowerInvariant()
    };

    public static string ToApiString(this ProjectRole role) => role switch
    {
        ProjectRole.Viewer => "viewer",
        ProjectRole.Editor => "editor",
        _ => role.ToString().ToLowerInvariant()
    };
}
