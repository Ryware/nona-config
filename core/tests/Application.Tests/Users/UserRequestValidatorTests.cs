using Nona.Application.Admin.Users.Commands;
using Nona.Application.Admin.Users.Validators;

namespace Nona.Application.Tests.Users;

public class UserRequestValidatorTests
{
    [Test]
    public async Task UpdateUser_AllowsAdminAndMemberRoles()
    {
        var validator = new UpdateUserRequestValidator();

        var result = await validator.ValidateAsync(new UpdateUserRequest("Admin", "admin", "all"));
        var member = await validator.ValidateAsync(new UpdateUserRequest("Member", "member", "all"));
        var roleOnly = await validator.ValidateAsync(new UpdateUserRequest(null, "admin", null));

        await Assert.That(result.IsValid).IsTrue();
        await Assert.That(member.IsValid).IsTrue();
        await Assert.That(roleOnly.IsValid).IsTrue();
    }

    [Test]
    public async Task CreateUser_AllowsAdminAndMemberButRejectsLegacyRoles()
    {
        var validator = new CreateUserRequestValidator();

        var admin = await validator.ValidateAsync(
            new CreateUserRequest("Admin", "new-admin@example.com", "admin", "all"));
        var member = await validator.ValidateAsync(
            new CreateUserRequest("Member", "new-member@example.com", "member", "all"));
        var viewer = await validator.ValidateAsync(
            new CreateUserRequest("Viewer", "viewer@example.com", "viewer", "all"));
        var editor = await validator.ValidateAsync(
            new CreateUserRequest("Editor", "editor@example.com", "editor", "all"));

        await Assert.That(admin.IsValid).IsTrue();
        await Assert.That(member.IsValid).IsTrue();
        await Assert.That(viewer.IsValid).IsFalse();
        await Assert.That(editor.IsValid).IsFalse();
        await Assert.That(viewer.Errors.Select(error => error.ErrorMessage))
            .Contains("Role must be 'admin' or 'member'");
    }
}
