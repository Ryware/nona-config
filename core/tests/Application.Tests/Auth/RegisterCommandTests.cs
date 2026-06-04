using MediatR;
using Nona.Application.Auth.Commands;
using Nona.Application.Auth.DTOs;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Entities;
using Nona.Domain.Interfaces;
using NSubstitute;

namespace Nona.Application.Tests.Auth;

public class RegisterCommandTests
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IDateTime _dateTime = Substitute.For<IDateTime>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();

    public RegisterCommandTests()
    {
        _dateTime.NowUtc.Returns(new DateTime(2026, 6, 3, 12, 0, 0, DateTimeKind.Utc));
        _passwordHasher.HashPassword("Password123!").Returns(("hashed-password", string.Empty));
    }

    [Test]
    public async Task FirstRegistration_ReturnsLoginToken()
    {
        _userRepository.ExistsAnyAsync(Arg.Any<CancellationToken>()).Returns(false);
        _mediator
            .Send(Arg.Any<LoginCommand>(), Arg.Any<CancellationToken>())
            .Returns(new LoginResult(
                true,
                new LoginResponse("jwt-token", "admin@example.com", "editor", new DateTime(2026, 6, 4, 12, 0, 0, DateTimeKind.Utc)),
                null));

        var handler = new RegisterCommandHandler(_mediator, _userRepository, _dateTime, _passwordHasher);

        var result = await handler.Handle(new RegisterCommand("admin@example.com", "Password123!"), CancellationToken.None);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Response).IsNotNull();
        await Assert.That(result.Response!.Token).IsEqualTo("jwt-token");
        await _userRepository.Received(1).AddAsync(Arg.Is<User>(user =>
            user.Email == "admin@example.com" &&
            user.IsAdmin &&
            user.PasswordHash == "hashed-password"), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task FirstRegistration_ReturnsFailure_WhenPostRegistrationLoginFails()
    {
        _userRepository.ExistsAnyAsync(Arg.Any<CancellationToken>()).Returns(false);
        _mediator
            .Send(Arg.Any<LoginCommand>(), Arg.Any<CancellationToken>())
            .Returns(new LoginResult(false, null, "Invalid username or password"));

        var handler = new RegisterCommandHandler(_mediator, _userRepository, _dateTime, _passwordHasher);

        var result = await handler.Handle(new RegisterCommand("admin@example.com", "Password123!"), CancellationToken.None);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Response).IsNull();
        await Assert.That(result.Error).IsEqualTo("Invalid username or password");
    }
}
