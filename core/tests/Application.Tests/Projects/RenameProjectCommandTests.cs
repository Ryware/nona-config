using Nona.Application.Admin.Projects.Commands;
using Nona.Application.Tests.Common;
using Nona.Domain.Entities;
using NSubstitute;

namespace Nona.Application.Tests.Projects;

public class RenameProjectCommandTests
{
    private const string CurrentName = "storefront";
    private const string NewName = "web-store";

    [Test]
    public async Task SystemAdmin_CanRenameProject()
    {
        var fixture = CreateFixture();
        fixture.SetupAsSystemAdmin();

        var result = await CreateHandler(fixture).Handle(
            new RenameProjectCommand("storefront-slug", NewName),
            CancellationToken.None);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Project!.Name).IsEqualTo(NewName);
        await Assert.That(result.Project.UrlSlug).IsEqualTo("storefront-slug");
        await Assert.That(result.Project.Environments).Contains("production");
        await fixture.ProjectRepository.Received(1).RenameAsync(
            CurrentName,
            NewName,
            fixture.DateTime.NowUtc,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task NonAdmin_CannotRenameProject()
    {
        var fixture = CreateFixture();
        fixture.SetupAsProjectAdmin("editor", CurrentName);

        var result = await CreateHandler(fixture).Handle(
            new RenameProjectCommand(CurrentName, NewName),
            CancellationToken.None);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Access denied. Only admin users can rename projects.");
        await fixture.ProjectRepository.DidNotReceiveWithAnyArgs()
            .RenameAsync(default!, default!, default, default);
    }

    [Test]
    public async Task MissingProject_CannotBeRenamed()
    {
        var fixture = new TestFixture();
        fixture.SetupAsSystemAdmin();

        var result = await CreateHandler(fixture).Handle(
            new RenameProjectCommand("missing", NewName),
            CancellationToken.None);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Project not found");
    }

    [Test]
    public async Task DuplicateProjectName_IsRejected()
    {
        var fixture = CreateFixture();
        fixture.SetupAsSystemAdmin();
        fixture.SetupProjectExists(NewName);

        var result = await CreateHandler(fixture).Handle(
            new RenameProjectCommand(CurrentName, NewName),
            CancellationToken.None);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Project already exists");
    }

    [Test]
    public async Task CaseOnlyRename_IsAllowed()
    {
        var fixture = CreateFixture();
        fixture.SetupAsSystemAdmin();

        var result = await CreateHandler(fixture).Handle(
            new RenameProjectCommand(CurrentName, "Storefront"),
            CancellationToken.None);

        await Assert.That(result.Success).IsTrue();
        await fixture.ProjectRepository.DidNotReceive()
            .ExistsAsync("Storefront", Arg.Any<CancellationToken>());
    }

    private static TestFixture CreateFixture()
    {
        var fixture = new TestFixture();
        fixture.ProjectRepository.GetByNameAsync(CurrentName, Arg.Any<CancellationToken>())
            .Returns(new Project
            {
                Id = 42,
                Name = CurrentName,
                UrlSlug = "storefront-slug",
                CreatedAt = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc)
            });
        fixture.ProjectRepository.GetByNameAsync("storefront-slug", Arg.Any<CancellationToken>())
            .Returns((Project?)null);
        fixture.ProjectRepository.ListAsync(Arg.Any<CancellationToken>())
            .Returns([
                new Project
                {
                    Id = 42,
                    Name = CurrentName,
                    UrlSlug = "storefront-slug",
                    CreatedAt = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc)
                }
            ]);
        fixture.ProjectRepository.ExistsAsync(NewName, Arg.Any<CancellationToken>()).Returns(false);
        fixture.EnvironmentRepository.ListByProjectAsync(CurrentName, Arg.Any<CancellationToken>())
            .Returns([new ProjectEnvironment { Project = CurrentName, Name = "production" }]);
        return fixture;
    }

    private static RenameProjectCommandHandler CreateHandler(TestFixture fixture)
    {
        return new RenameProjectCommandHandler(
            fixture.ProjectRepository,
            fixture.EnvironmentRepository,
            fixture.UserAuthorizationService,
            fixture.DateTime);
    }
}
