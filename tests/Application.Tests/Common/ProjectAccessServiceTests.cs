using Nona.Application.Common.Interfaces;
using Nona.Domain.Entities;
using Nona.Domain.Interfaces;
using NSubstitute;

namespace Nona.Application.Tests.Common;

public class ProjectAccessServiceTests
{
    private const string ProjectName = "test-project";
    private const string Username = "testuser";

    [Test]
    public async Task HasAccessAsync_SystemAdmin_ReturnsTrue()
    {
        // Arrange
        var currentUserService = Substitute.For<ICurrentUserService>();
        var projectMemberRepository = Substitute.For<IProjectMemberRepository>();

        currentUserService.IsAdmin.Returns(true);
        currentUserService.Username.Returns(Username);

        var service = new ProjectAccessService(currentUserService, projectMemberRepository);

        // Act
        var result = await service.HasAccessAsync(ProjectName);

        // Assert
        await Assert.That(result).IsTrue();
        // Should not check project member repository for admin users
        await projectMemberRepository.DidNotReceive().ExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task HasAccessAsync_ProjectMember_ReturnsTrue()
    {
        // Arrange
        var currentUserService = Substitute.For<ICurrentUserService>();
        var projectMemberRepository = Substitute.For<IProjectMemberRepository>();

        currentUserService.IsAdmin.Returns(false);
        currentUserService.Username.Returns(Username);
        projectMemberRepository.ExistsAsync(Username, ProjectName, Arg.Any<CancellationToken>()).Returns(true);

        var service = new ProjectAccessService(currentUserService, projectMemberRepository);

        // Act
        var result = await service.HasAccessAsync(ProjectName);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task HasAccessAsync_NonProjectMember_ReturnsFalse()
    {
        // Arrange
        var currentUserService = Substitute.For<ICurrentUserService>();
        var projectMemberRepository = Substitute.For<IProjectMemberRepository>();

        currentUserService.IsAdmin.Returns(false);
        currentUserService.Username.Returns(Username);
        projectMemberRepository.ExistsAsync(Username, ProjectName, Arg.Any<CancellationToken>()).Returns(false);

        var service = new ProjectAccessService(currentUserService, projectMemberRepository);

        // Act
        var result = await service.HasAccessAsync(ProjectName);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task HasAccessAsync_NoUsername_ReturnsFalse()
    {
        // Arrange
        var currentUserService = Substitute.For<ICurrentUserService>();
        var projectMemberRepository = Substitute.For<IProjectMemberRepository>();

        currentUserService.IsAdmin.Returns(false);
        currentUserService.Username.Returns((string?)null);

        var service = new ProjectAccessService(currentUserService, projectMemberRepository);

        // Act
        var result = await service.HasAccessAsync(ProjectName);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task HasAdminAccessAsync_SystemAdmin_ReturnsTrue()
    {
        // Arrange
        var currentUserService = Substitute.For<ICurrentUserService>();
        var projectMemberRepository = Substitute.For<IProjectMemberRepository>();

        currentUserService.IsAdmin.Returns(true);
        currentUserService.Username.Returns(Username);

        var service = new ProjectAccessService(currentUserService, projectMemberRepository);

        // Act
        var result = await service.HasAdminAccessAsync(ProjectName);

        // Assert
        await Assert.That(result).IsTrue();
        // Should not check project member repository for admin users
        await projectMemberRepository.DidNotReceive().GetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task HasAdminAccessAsync_ProjectAdmin_ReturnsTrue()
    {
        // Arrange
        var currentUserService = Substitute.For<ICurrentUserService>();
        var projectMemberRepository = Substitute.For<IProjectMemberRepository>();

        currentUserService.IsAdmin.Returns(false);
        currentUserService.Username.Returns(Username);
        projectMemberRepository.GetAsync(Username, ProjectName, Arg.Any<CancellationToken>())
            .Returns(new ProjectMember { Username = Username, ProjectName = ProjectName, Role = ProjectRole.Admin });

        var service = new ProjectAccessService(currentUserService, projectMemberRepository);

        // Act
        var result = await service.HasAdminAccessAsync(ProjectName);

        // Assert
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task HasAdminAccessAsync_ProjectUser_ReturnsFalse()
    {
        // Arrange
        var currentUserService = Substitute.For<ICurrentUserService>();
        var projectMemberRepository = Substitute.For<IProjectMemberRepository>();

        currentUserService.IsAdmin.Returns(false);
        currentUserService.Username.Returns(Username);
        projectMemberRepository.GetAsync(Username, ProjectName, Arg.Any<CancellationToken>())
            .Returns(new ProjectMember { Username = Username, ProjectName = ProjectName, Role = ProjectRole.User });

        var service = new ProjectAccessService(currentUserService, projectMemberRepository);

        // Act
        var result = await service.HasAdminAccessAsync(ProjectName);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task HasAdminAccessAsync_NonProjectMember_ReturnsFalse()
    {
        // Arrange
        var currentUserService = Substitute.For<ICurrentUserService>();
        var projectMemberRepository = Substitute.For<IProjectMemberRepository>();

        currentUserService.IsAdmin.Returns(false);
        currentUserService.Username.Returns(Username);
        projectMemberRepository.GetAsync(Username, ProjectName, Arg.Any<CancellationToken>())
            .Returns((ProjectMember?)null);

        var service = new ProjectAccessService(currentUserService, projectMemberRepository);

        // Act
        var result = await service.HasAdminAccessAsync(ProjectName);

        // Assert
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task HasAdminAccessAsync_NoUsername_ReturnsFalse()
    {
        // Arrange
        var currentUserService = Substitute.For<ICurrentUserService>();
        var projectMemberRepository = Substitute.For<IProjectMemberRepository>();

        currentUserService.IsAdmin.Returns(false);
        currentUserService.Username.Returns((string?)null);

        var service = new ProjectAccessService(currentUserService, projectMemberRepository);

        // Act
        var result = await service.HasAdminAccessAsync(ProjectName);

        // Assert
        await Assert.That(result).IsFalse();
    }
}
