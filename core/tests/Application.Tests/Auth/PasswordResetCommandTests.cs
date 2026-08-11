using Nona.Application.Admin.Users.Commands;
using Nona.Application.Auth;
using Nona.Application.Auth.Commands;
using Nona.Application.Auth.Queries;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Entities;
using Nona.Domain.Enums;
using Nona.Domain.Interfaces;
using NSubstitute;

namespace Nona.Application.Tests.Auth;

public class PasswordResetCommandTests
{
    private static readonly DateTime Now = new(2026, 8, 9, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task Generate_AsAdmin_StoresHashAndTwentyFourHourExpiry()
    {
        var repository = Substitute.For<IUserRepository>();
        var authorization = Substitute.For<IUserAuthorizationService>();
        var dateTime = Substitute.For<IDateTime>();
        var admin = User("admin@example.com", UserRole.Admin, "admin-hash");
        var target = User("user@example.com", UserRole.Viewer, "password-hash");
        authorization.GetCurrentUserAsync(Arg.Any<CancellationToken>()).Returns(admin);
        repository.GetByIdAsync(target.Id, Arg.Any<CancellationToken>()).Returns(target);
        dateTime.NowUtc.Returns(Now);
        var handler = new GeneratePasswordResetCommandHandler(repository, authorization, dateTime);

        var result = await handler.Handle(
            new GeneratePasswordResetCommand(target.Id),
            CancellationToken.None);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Response).IsNotNull();
        await Assert.That(result.Response!.ExpiresAt).IsEqualTo(Now.AddHours(24));
        await Assert.That(target.PasswordResetTokenHash).IsNotEqualTo(result.Response.PasswordResetToken);
        await Assert.That(target.PasswordResetTokenHash?.Length).IsEqualTo(64);
        await Assert.That(target.PasswordResetTokenExpiresAt).IsEqualTo(Now.AddHours(24));
        await repository.Received(1).UpdateAsync(target, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Generate_RejectsEditorAndPasswordlessAccount()
    {
        var repository = Substitute.For<IUserRepository>();
        var authorization = Substitute.For<IUserAuthorizationService>();
        var dateTime = Substitute.For<IDateTime>();
        var target = User("user@example.com", UserRole.Viewer, null);
        authorization.GetCurrentUserAsync(Arg.Any<CancellationToken>())
            .Returns(User("editor@example.com", UserRole.Editor, "hash"));
        repository.GetByIdAsync(target.Id, Arg.Any<CancellationToken>()).Returns(target);
        var handler = new GeneratePasswordResetCommandHandler(repository, authorization, dateTime);

        var editorResult = await handler.Handle(
            new GeneratePasswordResetCommand(target.Id),
            CancellationToken.None);

        await Assert.That(editorResult.Success).IsFalse();
        await Assert.That(editorResult.Error).IsEqualTo("Access denied");

        authorization.GetCurrentUserAsync(Arg.Any<CancellationToken>())
            .Returns(User("admin@example.com", UserRole.Admin, "hash"));
        var passwordlessResult = await handler.Handle(
            new GeneratePasswordResetCommand(target.Id),
            CancellationToken.None);

        await Assert.That(passwordlessResult.Success).IsFalse();
        await Assert.That(passwordlessResult.ErrorCode).IsEqualTo(AuthErrorCodes.PasswordResetUnavailable);
        await repository.DidNotReceive().UpdateAsync(target, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Generate_RejectsCurrentAdminAccount()
    {
        var repository = Substitute.For<IUserRepository>();
        var authorization = Substitute.For<IUserAuthorizationService>();
        var dateTime = Substitute.For<IDateTime>();
        var admin = User("admin@example.com", UserRole.Admin, "admin-hash");
        authorization.GetCurrentUserAsync(Arg.Any<CancellationToken>()).Returns(admin);
        var handler = new GeneratePasswordResetCommandHandler(repository, authorization, dateTime);

        var result = await handler.Handle(
            new GeneratePasswordResetCommand(admin.Id),
            CancellationToken.None);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.ErrorCode).IsEqualTo(AuthErrorCodes.PasswordResetSelfNotAllowed);
        await repository.DidNotReceive().GetByIdAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
        await repository.DidNotReceive().UpdateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Details_RejectExpiredToken()
    {
        var repository = Substitute.For<IUserRepository>();
        var dateTime = Substitute.For<IDateTime>();
        dateTime.NowUtc.Returns(Now);
        var expiredUser = User("user@example.com", UserRole.Viewer, "hash");
        expiredUser.PasswordResetTokenHash = "token-hash";
        expiredUser.PasswordResetTokenExpiresAt = Now;
        repository.GetByPasswordResetTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(expiredUser);
        var handler = new GetPasswordResetQueryHandler(repository, dateTime);

        var result = await handler.Handle(new GetPasswordResetQuery("token"), CancellationToken.None);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.ErrorCode).IsEqualTo(AuthErrorCodes.PasswordResetInvalidOrUsed);
    }

    [Test]
    public async Task Complete_HashesPasswordAndConsumesToken()
    {
        var repository = Substitute.For<IUserRepository>();
        var passwordHasher = Substitute.For<IPasswordHasher>();
        var dateTime = Substitute.For<IDateTime>();
        var auditLogService = Substitute.For<IAuditLogService>();
        var user = User("user@example.com", UserRole.Viewer, "old-hash");
        user.PasswordResetTokenHash = "token-hash";
        user.PasswordResetTokenExpiresAt = Now.AddHours(1);
        repository.GetByPasswordResetTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(user);
        repository.TryResetPasswordAsync(
                Arg.Any<string>(),
                Now,
                "new-hash",
                "new-salt",
                Now,
                Arg.Any<CancellationToken>())
            .Returns(true);
        passwordHasher.HashPassword("NewPassword123!").Returns(("new-hash", "new-salt"));
        dateTime.NowUtc.Returns(Now);
        var handler = new CompletePasswordResetCommandHandler(
            repository,
            passwordHasher,
            dateTime,
            auditLogService);

        var result = await handler.Handle(
            new CompletePasswordResetCommand("token", "NewPassword123!"),
            CancellationToken.None);

        await Assert.That(result.Success).IsTrue();
        await repository.Received(1).TryResetPasswordAsync(
            Arg.Any<string>(),
            Now,
            "new-hash",
            "new-salt",
            Now,
            Arg.Any<CancellationToken>());
        await auditLogService.Received(1).WriteAsAsync(
            "Password Reset Link",
            true,
            AuditActionKind.Activity,
            "Reset Password",
            user.Email,
            cancellationToken: Arg.Any<CancellationToken>());

        var auditArguments = auditLogService.ReceivedCalls().Single().GetArguments();
        await Assert.That(auditArguments).DoesNotContain("token");
        await Assert.That(auditArguments).DoesNotContain("NewPassword123!");
    }

    private static User User(string email, UserRole role, string? passwordHash) => new()
    {
        Id = Math.Abs(email.GetHashCode()),
        Email = email,
        Name = email,
        Role = role,
        PasswordHash = passwordHash,
        CreatedAt = Now,
        UpdatedAt = Now
    };
}
