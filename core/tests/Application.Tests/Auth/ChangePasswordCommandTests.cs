using Nona.Application.Auth;
using Nona.Application.Auth.Commands;
using Nona.Application.Auth.DTOs;
using Nona.Application.Auth.Queries;
using Nona.Application.Auth.Validators;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Entities;
using Nona.Domain.Enums;
using Nona.Domain.Interfaces;
using NSubstitute;

namespace Nona.Application.Tests.Auth;

public class ChangePasswordCommandTests
{
    private static readonly DateTime Now = new(2026, 8, 9, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task Validator_UsesSharedPolicyForNewPassword()
    {
        var validator = new ChangePasswordRequestValidator();

        var weak = validator.Validate(new ChangePasswordRequest("Password1!", "x"));
        var strong = validator.Validate(new ChangePasswordRequest("Password1!", "NewPassword1!"));

        await Assert.That(weak.IsValid).IsFalse();
        await Assert.That(weak.Errors.Single().ErrorMessage)
            .IsEqualTo("Password must be at least 8 characters long");
        await Assert.That(strong.IsValid).IsTrue();
    }

    [Test]
    public async Task ChangePassword_VerifiesCurrentPasswordAndClearsResetLink()
    {
        var authorization = Substitute.For<IUserAuthorizationService>();
        var repository = Substitute.For<IUserRepository>();
        var hasher = Substitute.For<IPasswordHasher>();
        var dateTime = Substitute.For<IDateTime>();
        var user = User("old-hash");
        user.PasswordResetTokenHash = "outstanding-reset";
        user.PasswordResetTokenExpiresAt = Now.AddHours(1);
        authorization.GetCurrentUserAsync(Arg.Any<CancellationToken>()).Returns(user);
        hasher.VerifyPassword("OldPassword123!", "old-hash").Returns(true);
        hasher.VerifyPassword("NewPassword123!", "old-hash").Returns(false);
        hasher.HashPassword("NewPassword123!").Returns(("new-hash", "new-salt"));
        dateTime.NowUtc.Returns(Now);
        var handler = new ChangePasswordCommandHandler(authorization, repository, hasher, dateTime);

        var result = await handler.Handle(
            new ChangePasswordCommand("OldPassword123!", "NewPassword123!"),
            CancellationToken.None);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(user.PasswordHash).IsEqualTo("new-hash");
        await Assert.That(user.PasswordResetTokenHash).IsNull();
        await Assert.That(user.PasswordResetTokenExpiresAt).IsNull();
        await repository.Received(1).UpdateAsync(user, Arg.Any<CancellationToken>());
    }

    [Test]
    [Arguments(null, "password_change_unavailable")]
    [Arguments("old-hash", "current_password_invalid")]
    public async Task ChangePassword_RejectsUnavailableOrIncorrectCurrentPassword(
        string? passwordHash,
        string expectedCode)
    {
        var authorization = Substitute.For<IUserAuthorizationService>();
        var repository = Substitute.For<IUserRepository>();
        var hasher = Substitute.For<IPasswordHasher>();
        var dateTime = Substitute.For<IDateTime>();
        var user = User(passwordHash);
        authorization.GetCurrentUserAsync(Arg.Any<CancellationToken>()).Returns(user);
        hasher.VerifyPassword(Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        var handler = new ChangePasswordCommandHandler(authorization, repository, hasher, dateTime);

        var result = await handler.Handle(
            new ChangePasswordCommand("wrong", "new"),
            CancellationToken.None);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.ErrorCode).IsEqualTo(expectedCode);
        await repository.DidNotReceive().UpdateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ChangePassword_RejectsSamePassword()
    {
        var authorization = Substitute.For<IUserAuthorizationService>();
        var repository = Substitute.For<IUserRepository>();
        var hasher = Substitute.For<IPasswordHasher>();
        var dateTime = Substitute.For<IDateTime>();
        var user = User("old-hash");
        authorization.GetCurrentUserAsync(Arg.Any<CancellationToken>()).Returns(user);
        hasher.VerifyPassword("same", "old-hash").Returns(true);
        var handler = new ChangePasswordCommandHandler(authorization, repository, hasher, dateTime);

        var result = await handler.Handle(
            new ChangePasswordCommand("same", "same"),
            CancellationToken.None);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.ErrorCode).IsEqualTo(AuthErrorCodes.NewPasswordMustDiffer);
        await repository.DidNotReceive().UpdateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task CurrentAccount_ReportsPasswordCapability()
    {
        var authorization = Substitute.For<IUserAuthorizationService>();
        authorization.GetCurrentUserAsync(Arg.Any<CancellationToken>()).Returns(User("hash"));
        var handler = new GetCurrentAccountQueryHandler(authorization);

        var result = await handler.Handle(new GetCurrentAccountQuery(), CancellationToken.None);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Account).IsNotNull();
        await Assert.That(result.Account!.PasswordEnabled).IsTrue();
        await Assert.That(result.Account.Email).IsEqualTo("user@example.com");
    }

    private static User User(string? passwordHash) => new()
    {
        Id = 42,
        Email = "user@example.com",
        Name = "User",
        Role = UserRole.Viewer,
        PasswordHash = passwordHash,
        CreatedAt = Now,
        UpdatedAt = Now
    };
}
