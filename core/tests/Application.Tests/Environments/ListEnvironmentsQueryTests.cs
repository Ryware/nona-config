using Nona.Application.Admin.Environments.Queries;
using Nona.Application.Tests.Common;
using Nona.Domain.Entities;
using NSubstitute;

namespace Nona.Application.Tests.Environments;

public class ListEnvironmentsQueryTests
{
    private const string ProjectName = "test-project";

    [Test]
    public async Task SystemAdmin_CanListEnvironments()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.SetupAsSystemAdmin();
        fixture.SetupProjectExists(ProjectName);

        var environments = new List<ProjectEnvironment>
        {
            new() { Name = "development", Project = ProjectName },
            new() { Name = "production", Project = ProjectName }
        };
        fixture.EnvironmentRepository.ListByProjectAsync(ProjectName, Arg.Any<CancellationToken>())
            .Returns(environments);

        var handler = new ListEnvironmentsQueryHandler(
            fixture.ProjectRepository,
            fixture.EnvironmentRepository,
            fixture.ProjectMemberRepository,
            fixture.CurrentUserService);

        var query = new ListEnvironmentsQuery(ProjectName);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Environments).IsNotNull();
        await Assert.That(result.Environments!.Count).IsEqualTo(2);
    }

    [Test]
    public async Task ProjectAdmin_CanListEnvironments()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.SetupAsProjectAdmin("projectadmin", ProjectName);
        fixture.SetupProjectExists(ProjectName);

        var environments = new List<ProjectEnvironment>
        {
            new() { Name = "development", Project = ProjectName }
        };
        fixture.EnvironmentRepository.ListByProjectAsync(ProjectName, Arg.Any<CancellationToken>())
            .Returns(environments);

        var handler = new ListEnvironmentsQueryHandler(
            fixture.ProjectRepository,
            fixture.EnvironmentRepository,
            fixture.ProjectMemberRepository,
            fixture.CurrentUserService);

        var query = new ListEnvironmentsQuery(ProjectName);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Environments).IsNotNull();
        await Assert.That(result.Environments!.Count).IsEqualTo(1);
    }

    [Test]
    public async Task ProjectUser_CanListEnvironments()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.SetupAsProjectUser("regularuser", ProjectName);
        fixture.SetupProjectExists(ProjectName);

        var environments = new List<ProjectEnvironment>
        {
            new() { Name = "development", Project = ProjectName }
        };
        fixture.EnvironmentRepository.ListByProjectAsync(ProjectName, Arg.Any<CancellationToken>())
            .Returns(environments);

        var handler = new ListEnvironmentsQueryHandler(
            fixture.ProjectRepository,
            fixture.EnvironmentRepository,
            fixture.ProjectMemberRepository,
            fixture.CurrentUserService);

        var query = new ListEnvironmentsQuery(ProjectName);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Environments).IsNotNull();
    }

    [Test]
    public async Task UserWithNoAccess_CannotListEnvironments()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.SetupAsUserWithNoProjectAccess("unauthorized", ProjectName);
        fixture.SetupProjectExists(ProjectName);

        var handler = new ListEnvironmentsQueryHandler(
            fixture.ProjectRepository,
            fixture.EnvironmentRepository,
            fixture.ProjectMemberRepository,
            fixture.CurrentUserService);

        var query = new ListEnvironmentsQuery(ProjectName);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Access denied");
    }

    [Test]
    public async Task ListEnvironments_ProjectNotFound_ReturnsFalse()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.SetupAsSystemAdmin();
        fixture.SetupProjectExists(ProjectName, exists: false);

        var handler = new ListEnvironmentsQueryHandler(
            fixture.ProjectRepository,
            fixture.EnvironmentRepository,
            fixture.ProjectMemberRepository,
            fixture.CurrentUserService);

        var query = new ListEnvironmentsQuery(ProjectName);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Project not found");
    }
}
