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

    public static KeyScope? ParseKeyScope(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "client" => KeyScope.Frontend,
        "server" => KeyScope.Backend,
        "all" => KeyScope.All,
        _ => null
    };

    public static string ToApiString(this UserRole role) => role switch
    {
        UserRole.Viewer => "viewer",
        UserRole.Editor => "editor",
        UserRole.Admin => "admin",
        _ => role.ToString().ToLowerInvariant()
    };

    public static bool TryParseApiRole(string? value, out UserRole role)
    {
        switch (value?.ToLowerInvariant())
        {
            case "viewer":
                role = UserRole.Viewer;
                return true;
            case "editor":
                role = UserRole.Editor;
                return true;
            default:
                role = default;
                return false;
        }
    }

    public static string ToApiString(this ProjectRole role) => role switch
    {
        ProjectRole.Viewer => "viewer",
        ProjectRole.Editor => "editor",
        _ => role.ToString().ToLowerInvariant()
    };
}
