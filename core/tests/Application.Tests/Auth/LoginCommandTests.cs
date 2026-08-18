using Nona.Application.Auth.Commands;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Entities;
using Nona.Domain.Interfaces;
using NSubstitute;

namespace Nona.Application.Tests.Auth;

public class LoginCommandTests
{
    [Test]
    public async Task Login_ReturnsExplicitAdminRole()
    {
        var userRepository = Substitute.For<IUserRepository>();
        var jwtTokenService = Substitute.For<IJwtTokenService>();
        var passwordHasher = Substitute.For<IPasswordHasher>();
        userRepository.GetAsync("admin@example.com", Arg.Any<CancellationToken>())
            .Returns(new User
            {
                Email = "admin@example.com",
                Name = "Admin",
                PasswordHash = "hashed-password",
                Role = UserRole.Admin
            });
        passwordHasher.VerifyPassword("Password123!", "hashed-password").Returns(true);
        jwtTokenService.GenerateToken(Arg.Any<User>()).Returns("jwt-token");

        var handler = new LoginCommandHandler(userRepository, jwtTokenService, passwordHasher);

        var result = await handler.Handle(
            new LoginCommand("admin@example.com", "Password123!"),
            CancellationToken.None);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Response!.Role).IsEqualTo("admin");
    }
}
