using Nona.Application.Admin.Users.Commands;
using Nona.Application.Admin.Users.Validators;

namespace Nona.Application.Tests.Users;

public class UserRequestValidatorTests
{
    [Test]
    public async Task UpdateUser_AllowsAdminRoleForHandlerImmutabilityCheck()
    {
        var validator = new UpdateUserRequestValidator();

        var result = await validator.ValidateAsync(new UpdateUserRequest("Admin", "admin", "all"));

        await Assert.That(result.IsValid).IsTrue();
    }

    [Test]
    public async Task CreateUser_StillRejectsAdminRole()
    {
        var validator = new CreateUserRequestValidator();

        var result = await validator.ValidateAsync(
            new CreateUserRequest("Admin", "new-admin@example.com", "admin", "all"));

        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Errors.Select(error => error.ErrorMessage))
            .Contains("Role must be 'viewer' or 'editor'");
    }
}
