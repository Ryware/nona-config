using Nona.Application.Auth;
using Nona.Application.Auth.Commands;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Entities;
using Nona.Domain.Enums;
using Nona.Domain.Interfaces;
using NSubstitute;

namespace Nona.Application.Tests.Auth;

public class CompleteInvitationWithPasswordCommandTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly IJwtTokenService _jwtTokenService = Substitute.For<IJwtTokenService>();
    private readonly IDateTime _dateTime = Substitute.For<IDateTime>();

    public CompleteInvitationWithPasswordCommandTests()
    {
        _passwordHasher.HashPassword("Password123!").Returns(("hashed-password", string.Empty));
        _jwtTokenService.GenerateToken(Arg.Any<User>()).Returns("invitation-token");
        _dateTime.NowUtc.Returns(new DateTime(2026, 4, 23, 12, 0, 0, DateTimeKind.Utc));
    }

    [Test]
    public async Task SetsPassword_ClearsInvite_AndReturnsLoginToken()
    {
        var user = CreateUser();
        _userRepository.GetByInviteTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(user);

        var handler = new CompleteInvitationWithPasswordCommandHandler(
            _userRepository,
            _passwordHasher,
            _jwtTokenService,
            _dateTime);

        var result = await handler.Handle(new CompleteInvitationWithPasswordCommand("invite-token", "Password123!"), CancellationToken.None);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Response).IsNotNull();
        await Assert.That(result.Response!.Token).IsEqualTo("invitation-token");
        await _userRepository.Received(1).UpdateAsync(Arg.Is<User>(candidate =>
            candidate.PasswordHash == "hashed-password" &&
            candidate.InviteTokenHash == null), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RejectsInvalidInviteToken()
    {
        _userRepository.GetByInviteTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var handler = new CompleteInvitationWithPasswordCommandHandler(
            _userRepository,
            _passwordHasher,
            _jwtTokenService,
            _dateTime);

        var result = await handler.Handle(new CompleteInvitationWithPasswordCommand("missing-token", "Password123!"), CancellationToken.None);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.ErrorCode).IsEqualTo(AuthErrorCodes.InvitationInvalidOrUsed);
        await _userRepository.DidNotReceive().UpdateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    private User CreateUser()
    {
        return new User
        {
            Id = 55,
            Email = "invitee@example.com",
            Name = "Invited User",
            Role = UserRole.Viewer,
            Scope = KeyScope.All,
            InviteTokenHash = "invite-hash",
            CreatedAt = new DateTime(2026, 4, 23, 10, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 4, 23, 10, 0, 0, DateTimeKind.Utc)
        };
    }
}
