using Nona.Application.Admin.Projects.Commands;
using Nona.Application.Tests.Common;
using Nona.Domain.Entities;
using NSubstitute;

namespace Nona.Application.Tests.Projects;

public class DeleteProjectResolutionTests
{
    [Test]
    public async Task DeleteProject_ResolvesAnExactNameWithoutListingProjects()
    {
        var fixture = new TestFixture();
        var project = new Project { Id = 10, Name = "Display Project", UrlSlug = "display-project" };
        fixture.SetupAsSystemAdmin();
        fixture.ProjectRepository.GetByNameAsync(project.Name, Arg.Any<CancellationToken>()).Returns(project);
        ConfigureDependencies(fixture, project.Name);

        var result = await CreateHandler(fixture).Handle(
            new DeleteProjectCommand(project.Name),
            CancellationToken.None);

        await Assert.That(result.Success).IsTrue();
        await fixture.ProjectRepository.Received(1).DeleteAsync(project.Name, Arg.Any<CancellationToken>());
        await fixture.ProjectRepository.DidNotReceive().ListAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DeleteProject_ResolvesANameFromTheProjectList()
    {
        var fixture = new TestFixture();
        var project = new Project { Id = 11, Name = "Display Project", UrlSlug = "display-project" };
        fixture.SetupAsSystemAdmin();
        fixture.ProjectRepository.GetByNameAsync("display project", Arg.Any<CancellationToken>()).Returns((Project?)null);
        fixture.ProjectRepository.ListAsync(Arg.Any<CancellationToken>()).Returns([project]);
        ConfigureDependencies(fixture, project.Name);

        var result = await CreateHandler(fixture).Handle(
            new DeleteProjectCommand("display project"),
            CancellationToken.None);

        await Assert.That(result.Success).IsTrue();
        await fixture.ProjectRepository.Received(1).DeleteAsync(project.Name, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DeleteProject_ResolvesANumericId()
    {
        var fixture = new TestFixture();
        var project = new Project { Id = 12, Name = "Display Project", UrlSlug = "display-project" };
        fixture.SetupAsSystemAdmin();
        fixture.ProjectRepository.GetByNameAsync("12", Arg.Any<CancellationToken>()).Returns((Project?)null);
        fixture.ProjectRepository.ListAsync(Arg.Any<CancellationToken>()).Returns([project]);
        ConfigureDependencies(fixture, project.Name);

        var result = await CreateHandler(fixture).Handle(
            new DeleteProjectCommand("12"),
            CancellationToken.None);

        await Assert.That(result.Success).IsTrue();
        await fixture.ProjectRepository.Received(1).DeleteAsync(project.Name, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DeleteProject_ResolvesASlug()
    {
        var fixture = new TestFixture();
        var project = new Project { Id = 13, Name = "Display Project", UrlSlug = "display-project" };
        fixture.SetupAsSystemAdmin();
        fixture.ProjectRepository.GetByNameAsync("DISPLAY-PROJECT", Arg.Any<CancellationToken>()).Returns((Project?)null);
        fixture.ProjectRepository.ListAsync(Arg.Any<CancellationToken>()).Returns([project]);
        ConfigureDependencies(fixture, project.Name);

        var result = await CreateHandler(fixture).Handle(
            new DeleteProjectCommand("DISPLAY-PROJECT"),
            CancellationToken.None);

        await Assert.That(result.Success).IsTrue();
        await fixture.ProjectRepository.Received(1).DeleteAsync(project.Name, Arg.Any<CancellationToken>());
    }

    private static DeleteProjectCommandHandler CreateHandler(TestFixture fixture)
    {
        return new DeleteProjectCommandHandler(
            fixture.ProjectRepository,
            fixture.EnvironmentRepository,
            fixture.ConfigEntryRepository,
            fixture.ProjectMemberRepository,
            fixture.UserAuthorizationService);
    }

    private static void ConfigureDependencies(TestFixture fixture, string projectName)
    {
        fixture.EnvironmentRepository.ListByProjectAsync(projectName, Arg.Any<CancellationToken>()).Returns([]);
        fixture.ConfigEntryRepository.ListByProjectAsync(projectName, Arg.Any<CancellationToken>()).Returns([]);
    }
}
