using Nona.Application.Admin.Users.Queries;
using Nona.Application.Tests.Common;
using Nona.Domain.Entities;
using Nona.Domain.Enums;
using NSubstitute;

namespace Nona.Application.Tests.Users;

public class ListUsersQueryTests
{
    [Test]
    public async Task ListUsers_MapsProjectAccessUsingOneBatchMembershipRead()
    {
        var fixture = new TestFixture();
        var users = new List<User>
        {
            new()
            {
                Id = 1,
                Email = "first@example.com",
                Name = "First User",
                Role = UserRole.Viewer,
                Scope = KeyScope.Frontend
            },
            new()
            {
                Id = 2,
                Email = "second@example.com",
                Name = "Second User",
                Role = UserRole.Editor,
                Scope = KeyScope.All
            }
        };
        var memberships = new List<ProjectMember>
        {
            new() { Username = "first@example.com", ProjectId = "alpha", Role = ProjectRole.Editor },
            new() { Username = "first@example.com", ProjectId = "beta", Role = ProjectRole.Viewer },
            new() { Username = "second@example.com", ProjectId = "gamma", Role = ProjectRole.Viewer }
        };
        fixture.UserRepository.ListAsync(Arg.Any<CancellationToken>()).Returns(users);
        fixture.ProjectMemberRepository.ListByUsersAsync(
                Arg.Is<IReadOnlyCollection<string>>(emails =>
                    emails.Count == 2
                    && emails.Contains("first@example.com", StringComparer.OrdinalIgnoreCase)
                    && emails.Contains("second@example.com", StringComparer.OrdinalIgnoreCase)),
                Arg.Any<CancellationToken>())
            .Returns(memberships);

        var handler = new ListUsersQueryHandler(fixture.UserRepository, fixture.ProjectMemberRepository);

        var result = await handler.Handle(new ListUsersQuery(), CancellationToken.None);

        await Assert.That(result.Count).IsEqualTo(2);
        await Assert.That(result[0].Role).IsEqualTo("viewer");
        await Assert.That(result[0].Scope).IsEqualTo("client");
        await Assert.That(result[0].Projects.Select(project => project.ProjectName))
            .IsEquivalentTo(["alpha", "beta"]);
        await Assert.That(result[0].Projects.Select(project => project.Role))
            .IsEquivalentTo(["editor", "viewer"]);
        await Assert.That(result[1].Role).IsEqualTo("editor");
        await Assert.That(result[1].Projects.Single().ProjectName).IsEqualTo("gamma");

        await fixture.ProjectMemberRepository.Received(1).ListByUsersAsync(
            Arg.Any<IReadOnlyCollection<string>>(),
            Arg.Any<CancellationToken>());
        await fixture.ProjectMemberRepository.DidNotReceive().ListByUserAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ListUsers_SkipsMembershipReadWhenThereAreNoUsers()
    {
        var fixture = new TestFixture();
        fixture.UserRepository.ListAsync(Arg.Any<CancellationToken>()).Returns([]);
        var handler = new ListUsersQueryHandler(fixture.UserRepository, fixture.ProjectMemberRepository);

        var result = await handler.Handle(new ListUsersQuery(), CancellationToken.None);

        await Assert.That(result).IsEmpty();
        await fixture.ProjectMemberRepository.DidNotReceive().ListByUsersAsync(
            Arg.Any<IReadOnlyCollection<string>>(),
            Arg.Any<CancellationToken>());
    }
}
