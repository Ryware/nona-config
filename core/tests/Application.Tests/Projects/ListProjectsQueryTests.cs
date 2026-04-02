using Nona.Application.Admin.Projects.Queries;
using Nona.Application.Tests.Common;
using Nona.Domain.Entities;
using NSubstitute;

namespace Nona.Application.Tests.Projects;

public class ListProjectsQueryTests
{
    [Test]
    public async Task SystemAdmin_CanListAllProjects()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.SetupAsSystemAdmin();

        var projects = new List<Project>
        {
            new() { Name = "project1" },
            new() { Name = "project2" },
            new() { Name = "project3" }
        };
        fixture.ProjectRepository.ListAsync(Arg.Any<CancellationToken>()).Returns(projects);

        var handler = new ListProjectsQueryHandler(
            fixture.ProjectRepository,
            fixture.ProjectMemberRepository,
            fixture.CurrentUserService);

        var query = new ListProjectsQuery();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        await Assert.That(result.Count).IsEqualTo(3);
    }

    [Test]
    public async Task ProjectAdmin_CanListOnlyAssignedProjects()
    {
        // Arrange
        var fixture = new TestFixture();
        var username = "projectadmin";
        var assignedProjectName = "project1";
        fixture.SetupAsProjectAdmin(username, assignedProjectName);

        var projects = new List<Project>
        {
            new() { Name = "project1" },
            new() { Name = "project2" },
            new() { Name = "project3" }
        };
        fixture.ProjectRepository.ListAsync(Arg.Any<CancellationToken>()).Returns(projects);

        var userProjects = new List<ProjectMember>
        {
            new() { Username = username, ProjectId = assignedProjectName, Role = ProjectRole.Editor }
        };
        fixture.ProjectMemberRepository.ListByUserAsync(username, Arg.Any<CancellationToken>()).Returns(userProjects);

        var handler = new ListProjectsQueryHandler(
            fixture.ProjectRepository,
            fixture.ProjectMemberRepository,
            fixture.CurrentUserService);

        var query = new ListProjectsQuery();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].Name).IsEqualTo(assignedProjectName);
    }

    [Test]
    public async Task ProjectUser_CanListOnlyAssignedProjects()
    {
        // Arrange
        var fixture = new TestFixture();
        var username = "regularuser";
        var assignedProjectName = "project2";
        fixture.SetupAsProjectUser(username, assignedProjectName);

        var projects = new List<Project>
        {
            new() { Name = "project1" },
            new() { Name = "project2" },
            new() { Name = "project3" }
        };
        fixture.ProjectRepository.ListAsync(Arg.Any<CancellationToken>()).Returns(projects);

        var userProjects = new List<ProjectMember>
        {
            new() { Username = username, ProjectId = assignedProjectName, Role = ProjectRole.Viewer }
        };
        fixture.ProjectMemberRepository.ListByUserAsync(username, Arg.Any<CancellationToken>()).Returns(userProjects);

        var handler = new ListProjectsQueryHandler(
            fixture.ProjectRepository,
            fixture.ProjectMemberRepository,
            fixture.CurrentUserService);

        var query = new ListProjectsQuery();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].Name).IsEqualTo(assignedProjectName);
    }

    [Test]
    public async Task UserWithMultipleProjectAssignments_CanListAllAssignedProjects()
    {
        // Arrange
        var fixture = new TestFixture();
        var username = "multiuser";
        fixture.CurrentUserService.Username.Returns(username);
        fixture.CurrentUserService.IsAdmin.Returns(false);

        var projects = new List<Project>
        {
            new() { Name = "project1" },
            new() { Name = "project2" },
            new() { Name = "project3" }
        };
        fixture.ProjectRepository.ListAsync(Arg.Any<CancellationToken>()).Returns(projects);

        var userProjects = new List<ProjectMember>
        {
            new() { Username = username, ProjectId = "project1", Role = ProjectRole.Editor },
            new() { Username = username, ProjectId = "project3", Role = ProjectRole.Viewer }
        };
        fixture.ProjectMemberRepository.ListByUserAsync(username, Arg.Any<CancellationToken>()).Returns(userProjects);

        var handler = new ListProjectsQueryHandler(
            fixture.ProjectRepository,
            fixture.ProjectMemberRepository,
            fixture.CurrentUserService);

        var query = new ListProjectsQuery();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        await Assert.That(result.Count).IsEqualTo(2);
        var projectNames = result.Select(p => p.Name).ToList();
        await Assert.That(projectNames).Contains("project1");
        await Assert.That(projectNames).Contains("project3");
    }

    [Test]
    public async Task UserWithNoProjectAssignments_ReturnsEmptyList()
    {
        // Arrange
        var fixture = new TestFixture();
        var username = "noassignments";
        fixture.CurrentUserService.Username.Returns(username);
        fixture.CurrentUserService.IsAdmin.Returns(false);

        var projects = new List<Project>
        {
            new() { Name = "project1" },
            new() { Name = "project2" }
        };
        fixture.ProjectRepository.ListAsync(Arg.Any<CancellationToken>()).Returns(projects);
        fixture.ProjectMemberRepository.ListByUserAsync(username, Arg.Any<CancellationToken>())
            .Returns(new List<ProjectMember>());

        var handler = new ListProjectsQueryHandler(
            fixture.ProjectRepository,
            fixture.ProjectMemberRepository,
            fixture.CurrentUserService);

        var query = new ListProjectsQuery();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        await Assert.That(result.Count).IsEqualTo(0);
    }

    [Test]
    public async Task UserWithNoUsername_ReturnsEmptyList()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.CurrentUserService.Username.Returns((string?)null);
        fixture.CurrentUserService.IsAdmin.Returns(false);

        var projects = new List<Project>
        {
            new() { Name = "project1" }
        };
        fixture.ProjectRepository.ListAsync(Arg.Any<CancellationToken>()).Returns(projects);

        var handler = new ListProjectsQueryHandler(
            fixture.ProjectRepository,
            fixture.ProjectMemberRepository,
            fixture.CurrentUserService);

        var query = new ListProjectsQuery();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        await Assert.That(result.Count).IsEqualTo(0);
    }
}
