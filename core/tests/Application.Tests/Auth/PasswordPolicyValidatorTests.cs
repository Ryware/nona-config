using Nona.Application.Auth.Commands;
using Nona.Application.Auth.DTOs;
using Nona.Application.Auth.Validators;

namespace Nona.Application.Tests.Auth;

public class PasswordPolicyValidatorTests
{
    [Test]
    [Arguments("", "Password is required")]
    [Arguments("Ab1!", "Password must be at least 8 characters long")]
    [Arguments("password1!", "Password must contain at least one uppercase letter")]
    [Arguments("Password!", "Password must contain at least one number")]
    [Arguments("Password1", "Password must contain at least one special character")]
    public async Task RegistrationInvitationAndResetRejectPasswordsOutsideSharedPolicy(
        string password,
        string expectedError)
    {
        var registration = new RegisterCommandValidator()
            .Validate(new RegisterCommand("user@example.com", password));
        var invitation = new CompleteInvitationPasswordRequestValidator()
            .Validate(new CompleteInvitationPasswordRequest(password));
        var reset = new ResetPasswordRequestValidator()
            .Validate(new ResetPasswordRequest(password));

        await Assert.That(registration.Errors.Single().ErrorMessage).IsEqualTo(expectedError);
        await Assert.That(invitation.Errors.Single().ErrorMessage).IsEqualTo(expectedError);
        await Assert.That(reset.Errors.Single().ErrorMessage).IsEqualTo(expectedError);
    }

    [Test]
    public async Task RegistrationInvitationAndResetAcceptPasswordMatchingSharedPolicy()
    {
        const string password = "Password1!";

        var registration = new RegisterCommandValidator()
            .Validate(new RegisterCommand("user@example.com", password));
        var invitation = new CompleteInvitationPasswordRequestValidator()
            .Validate(new CompleteInvitationPasswordRequest(password));
        var reset = new ResetPasswordRequestValidator()
            .Validate(new ResetPasswordRequest(password));

        await Assert.That(registration.IsValid).IsTrue();
        await Assert.That(invitation.IsValid).IsTrue();
        await Assert.That(reset.IsValid).IsTrue();
    }
}
