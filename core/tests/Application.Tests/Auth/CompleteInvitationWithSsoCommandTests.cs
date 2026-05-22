using Microsoft.Extensions.Logging.Abstractions;
using Nona.Application.Auth;
using Nona.Application.Auth.Commands;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Entities;
using Nona.Domain.Enums;
using Nona.Domain.Interfaces;
using NSubstitute;

namespace Nona.Application.Tests.Auth;

public class CompleteInvitationWithSsoCommandTests
{
    private readonly ISsoTokenValidator _validator = Substitute.For<ISsoTokenValidator>();
    private readonly IExternalIdentityRepository _externalIdentityRepository = Substitute.For<IExternalIdentityRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IJwtTokenService _jwtTokenService = Substitute.For<IJwtTokenService>();
    private readonly IDateTime _dateTime = Substitute.For<IDateTime>();

    public CompleteInvitationWithSsoCommandTests()
    {
        _validator.Provider.Returns(SsoProviders.Google);
        _validator.IsEnabled.Returns(true);
        _jwtTokenService.GenerateToken(Arg.Any<User>()).Returns("jwt-token");
        _dateTime.NowUtc.Returns(new DateTime(2026, 4, 23, 12, 0, 0, DateTimeKind.Utc));
    }

    [Test]
    public async Task RejectsInviteSso_WhenValidatedEmailDoesNotMatchInvite()
    {
        var invitedUser = CreateUser("invitee@example.com");

        _userRepository.GetByInviteTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(invitedUser);
        _validator.ValidateAsync("valid-token", Arg.Any<CancellationToken>())
            .Returns(SsoTokenValidationResult.Succeeded(new SsoIdentity(
                SsoProviders.Google,
                "subject-1",
                "https://accounts.google.com",
                "other@example.com",
                "Other User",
                null,
                true)));
        _externalIdentityRepository.GetAsync(SsoProviders.Google, "https://accounts.google.com", "subject-1", Arg.Any<CancellationToken>())
            .Returns((ExternalIdentity?)null);

        var handler = new CompleteInvitationWithSsoCommandHandler(
            [_validator],
            _externalIdentityRepository,
            _userRepository,
            _jwtTokenService,
            _dateTime,
            NullLogger<CompleteInvitationWithSsoCommandHandler>.Instance);

        var result = await handler.Handle(new CompleteInvitationWithSsoCommand("invite-token", SsoProviders.Google, "valid-token"), CancellationToken.None);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.ErrorCode).IsEqualTo(AuthErrorCodes.InvitationSsoEmailMismatch);
        await _userRepository.DidNotReceive().UpdateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    private static User CreateUser(string email)
    {
        return new User
        {
            Id = 99,
            Email = email,
            Name = "Invited User",
            Role = UserRole.Viewer,
            Scope = KeyScope.All,
            InviteTokenHash = "invite-hash",
            CreatedAt = new DateTime(2026, 4, 23, 9, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 4, 23, 9, 0, 0, DateTimeKind.Utc)
        };
    }
}
