using Nona.Application.Common;
using Nona.Domain.Entities;

namespace Nona.Application.Tests.Common;

public class EnumExtensionsTests
{
    [Test]
    [Arguments(UserRole.Viewer, "viewer")]
    [Arguments(UserRole.Editor, "editor")]
    [Arguments(UserRole.Admin, "admin")]
    public async Task ToApiString_ReturnsExplicitRoleName(UserRole role, string expected)
    {
        await Assert.That(role.ToApiString()).IsEqualTo(expected);
    }

    [Test]
    [Arguments("viewer", UserRole.Viewer)]
    [Arguments("EDITOR", UserRole.Editor)]
    public async Task TryParseApiRole_AcceptsSupportedRoles(string value, UserRole expectedRole)
    {
        var parsed = EnumExtensions.TryParseApiRole(value, out var role);

        await Assert.That(parsed).IsTrue();
        await Assert.That(role).IsEqualTo(expectedRole);
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("admin")]
    [Arguments(" viewer ")]
    public async Task TryParseApiRole_RejectsUnsupportedRoles(string? value)
    {
        var parsed = EnumExtensions.TryParseApiRole(value, out _);

        await Assert.That(parsed).IsFalse();
    }
}
