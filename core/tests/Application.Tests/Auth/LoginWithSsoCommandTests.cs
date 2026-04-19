using Microsoft.Extensions.Logging.Abstractions;
using Nona.Application.Auth;
using Nona.Application.Auth.Commands;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Entities;
using Nona.Domain.Enums;
using Nona.Domain.Interfaces;
using NSubstitute;

namespace Nona.Application.Tests.Auth;

public class LoginWithSsoCommandTests
{
    private readonly ISsoTokenValidator _validator = Substitute.For<ISsoTokenValidator>();
    private readonly IExternalIdentityRepository _externalIdentityRepository = Substitute.For<IExternalIdentityRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IJwtTokenService _jwtTokenService = Substitute.For<IJwtTokenService>();
    private readonly IDateTime _dateTime = Substitute.For<IDateTime>();

    public LoginWithSsoCommandTests()
    {
        _validator.Provider.Returns(SsoProviders.Google);
        _validator.IsEnabled.Returns(true);
        _jwtTokenService.GenerateToken(Arg.Any<User>()).Returns("internal-token");
        _dateTime.NowUtc.Returns(new DateTime(2026, 4, 16, 12, 0, 0, DateTimeKind.Utc));
    }

    [Test]
    public async Task ReturnsInternalToken_WhenMatchingExternalIdentityExists()
    {
        var user = CreateUser("admin@example.com");
        var identity = new ExternalIdentity
        {
            Id = 11,
            Provider = SsoProviders.Google,
            Issuer = "https://accounts.google.com",
            Subject = "google-subject",
            UserEmail = user.Email,
            CreatedAt = _dateTime.NowUtc,
            UpdatedAt = _dateTime.NowUtc
        };

        _validator.ValidateAsync("valid-token", Arg.Any<CancellationToken>())
            .Returns(SsoTokenValidationResult.Succeeded(CreateIdentity(email: user.Email)));
        _externalIdentityRepository.GetAsync(SsoProviders.Google, "https://accounts.google.com", "google-subject", Arg.Any<CancellationToken>())
            .Returns(identity);
        _userRepository.GetAsync(user.Email, Arg.Any<CancellationToken>()).Returns(user);

        var handler = CreateHandler();
        var result = await handler.Handle(new LoginWithSsoCommand(SsoProviders.Google, "valid-token"), CancellationToken.None);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Response).IsNotNull();
        await Assert.That(result.Response!.Token).IsEqualTo("internal-token");
        await _externalIdentityRepository.Received(1).UpdateAsync(Arg.Is<ExternalIdentity>(candidate =>
            candidate.Id == identity.Id && candidate.LastLoginAt == _dateTime.NowUtc), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task LinksExistingUser_OnFirstSuccessfulSsoLogin()
    {
        var user = CreateUser("member@example.com");

        _validator.ValidateAsync("valid-token", Arg.Any<CancellationToken>())
            .Returns(SsoTokenValidationResult.Succeeded(CreateIdentity(email: user.Email, subject: "subject-123")));
        _externalIdentityRepository.GetAsync(SsoProviders.Google, "https://accounts.google.com", "subject-123", Arg.Any<CancellationToken>())
            .Returns((ExternalIdentity?)null);
        _externalIdentityRepository.ListByUserEmailAsync(user.Email, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ExternalIdentity>());
        _userRepository.GetAsync(user.Email, Arg.Any<CancellationToken>()).Returns(user);

        var handler = CreateHandler();
        var result = await handler.Handle(new LoginWithSsoCommand(SsoProviders.Google, "valid-token"), CancellationToken.None);

        await Assert.That(result.Success).IsTrue();
        await _externalIdentityRepository.Received(1).AddAsync(Arg.Is<ExternalIdentity>(candidate =>
            candidate.Provider == SsoProviders.Google &&
            candidate.Subject == "subject-123" &&
            candidate.UserEmail == user.Email), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RejectsUnknownLocalUser()
    {
        _validator.ValidateAsync("valid-token", Arg.Any<CancellationToken>())
            .Returns(SsoTokenValidationResult.Succeeded(CreateIdentity(email: "unknown@example.com")));
        _externalIdentityRepository.GetAsync(SsoProviders.Google, "https://accounts.google.com", "google-subject", Arg.Any<CancellationToken>())
            .Returns((ExternalIdentity?)null);
        _userRepository.GetAsync("unknown@example.com", Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var handler = CreateHandler();
        var result = await handler.Handle(new LoginWithSsoCommand(SsoProviders.Google, "valid-token"), CancellationToken.None);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Authentication failed");
        await Assert.That(result.ErrorCode).IsEqualTo("sso_user_not_registered");
        await _externalIdentityRepository.DidNotReceive().AddAsync(Arg.Any<ExternalIdentity>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RejectsIdentityMismatch_WhenUserAlreadyHasLinkedProvider()
    {
        var user = CreateUser("member@example.com");
        var now = _dateTime.NowUtc;

        _validator.ValidateAsync("valid-token", Arg.Any<CancellationToken>())
            .Returns(SsoTokenValidationResult.Succeeded(CreateIdentity(email: user.Email, subject: "new-subject")));
        _externalIdentityRepository.GetAsync(SsoProviders.Google, "https://accounts.google.com", "new-subject", Arg.Any<CancellationToken>())
            .Returns((ExternalIdentity?)null);
        _userRepository.GetAsync(user.Email, Arg.Any<CancellationToken>()).Returns(user);
        _externalIdentityRepository.ListByUserEmailAsync(user.Email, Arg.Any<CancellationToken>())
            .Returns(
            [
                new ExternalIdentity
                {
                    Id = 99,
                    Provider = SsoProviders.Google,
                    Issuer = "https://accounts.google.com",
                    Subject = "old-subject",
                    UserEmail = user.Email,
                    CreatedAt = now,
                    UpdatedAt = now
                }
            ]);

        var handler = CreateHandler();
        var result = await handler.Handle(new LoginWithSsoCommand(SsoProviders.Google, "valid-token"), CancellationToken.None);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Authentication failed");
        await Assert.That(result.ErrorCode).IsNull();
        await _externalIdentityRepository.DidNotReceive().AddAsync(Arg.Any<ExternalIdentity>(), Arg.Any<CancellationToken>());
    }

    private LoginWithSsoCommandHandler CreateHandler()
    {
        return new LoginWithSsoCommandHandler(
            [_validator],
            _externalIdentityRepository,
            _userRepository,
            _jwtTokenService,
            _dateTime,
            NullLogger<LoginWithSsoCommandHandler>.Instance);
    }

    private static SsoIdentity CreateIdentity(string email, string subject = "google-subject")
    {
        return new SsoIdentity(
            SsoProviders.Google,
            subject,
            "https://accounts.google.com",
            email,
            "Example User",
            null,
            true);
    }

    private static User CreateUser(string email)
    {
        return new User
        {
            Id = 42,
            Email = email,
            Name = "Example User",
            Role = UserRole.Editor,
            Scope = KeyScope.All,
            CreatedAt = new DateTime(2026, 4, 16, 10, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 4, 16, 10, 0, 0, DateTimeKind.Utc)
        };
    }
}
