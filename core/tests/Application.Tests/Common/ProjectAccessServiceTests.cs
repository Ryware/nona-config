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
    public async Task HasAccessAsync_GlobalUser_ReturnsTrue()
    {
        var currentUserService = Substitute.For<ICurrentUserService>();
        var userAuthorizationService = Substitute.For<IUserAuthorizationService>();
        var projectMemberRepository = Substitute.For<IProjectMemberRepository>();

        userAuthorizationService.HasGlobalProjectAccessAsync(Arg.Any<CancellationToken>()).Returns(true);

        var service = new ProjectAccessService(currentUserService, userAuthorizationService, projectMemberRepository);

        var result = await service.HasViewAccessAsync(ProjectName);

        await Assert.That(result).IsTrue();
        await projectMemberRepository.DidNotReceive().ExistsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task HasAccessAsync_ProjectMember_ReturnsTrue()
    {
        var currentUserService = Substitute.For<ICurrentUserService>();
        var userAuthorizationService = Substitute.For<IUserAuthorizationService>();
        var projectMemberRepository = Substitute.For<IProjectMemberRepository>();

        userAuthorizationService.HasGlobalProjectAccessAsync(Arg.Any<CancellationToken>()).Returns(false);
        userAuthorizationService.GetCurrentUserAsync(Arg.Any<CancellationToken>())
            .Returns(new User { Email = Username, Name = Username, Role = UserRole.Viewer });
        projectMemberRepository.ExistsAsync(Username, ProjectName, Arg.Any<CancellationToken>()).Returns(true);

        var service = new ProjectAccessService(currentUserService, userAuthorizationService, projectMemberRepository);

        var result = await service.HasViewAccessAsync(ProjectName);

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task HasAccessAsync_NonProjectMember_ReturnsFalse()
    {
        var currentUserService = Substitute.For<ICurrentUserService>();
        var userAuthorizationService = Substitute.For<IUserAuthorizationService>();
        var projectMemberRepository = Substitute.For<IProjectMemberRepository>();

        userAuthorizationService.HasGlobalProjectAccessAsync(Arg.Any<CancellationToken>()).Returns(false);
        userAuthorizationService.GetCurrentUserAsync(Arg.Any<CancellationToken>())
            .Returns(new User { Email = Username, Name = Username, Role = UserRole.Viewer });
        projectMemberRepository.ExistsAsync(Username, ProjectName, Arg.Any<CancellationToken>()).Returns(false);

        var service = new ProjectAccessService(currentUserService, userAuthorizationService, projectMemberRepository);

        var result = await service.HasViewAccessAsync(ProjectName);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task HasAccessAsync_NoUsername_ReturnsFalse()
    {
        var currentUserService = Substitute.For<ICurrentUserService>();
        var userAuthorizationService = Substitute.For<IUserAuthorizationService>();
        var projectMemberRepository = Substitute.For<IProjectMemberRepository>();

        userAuthorizationService.HasGlobalProjectAccessAsync(Arg.Any<CancellationToken>()).Returns(false);
        userAuthorizationService.GetCurrentUserAsync(Arg.Any<CancellationToken>()).Returns((User?)null);
        currentUserService.Username.Returns((string?)null);

        var service = new ProjectAccessService(currentUserService, userAuthorizationService, projectMemberRepository);

        var result = await service.HasViewAccessAsync(ProjectName);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task HasAdminAccessAsync_GlobalUser_ReturnsTrue()
    {
        var currentUserService = Substitute.For<ICurrentUserService>();
        var userAuthorizationService = Substitute.For<IUserAuthorizationService>();
        var projectMemberRepository = Substitute.For<IProjectMemberRepository>();

        userAuthorizationService.HasGlobalProjectAccessAsync(Arg.Any<CancellationToken>()).Returns(true);

        var service = new ProjectAccessService(currentUserService, userAuthorizationService, projectMemberRepository);

        var result = await service.HasEditAccessAsync(ProjectName);

        await Assert.That(result).IsTrue();
        await projectMemberRepository.DidNotReceive().GetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task HasAdminAccessAsync_ProjectAdmin_ReturnsTrue()
    {
        var currentUserService = Substitute.For<ICurrentUserService>();
        var userAuthorizationService = Substitute.For<IUserAuthorizationService>();
        var projectMemberRepository = Substitute.For<IProjectMemberRepository>();

        userAuthorizationService.HasGlobalProjectAccessAsync(Arg.Any<CancellationToken>()).Returns(false);
        userAuthorizationService.GetCurrentUserAsync(Arg.Any<CancellationToken>())
            .Returns(new User { Email = Username, Name = Username, Role = UserRole.Viewer });
        projectMemberRepository.GetAsync(Username, ProjectName, Arg.Any<CancellationToken>())
            .Returns(new ProjectMember { Username = Username, ProjectId = ProjectName, Role = ProjectRole.Editor });

        var service = new ProjectAccessService(currentUserService, userAuthorizationService, projectMemberRepository);

        var result = await service.HasEditAccessAsync(ProjectName);

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task HasAdminAccessAsync_ProjectUser_ReturnsFalse()
    {
        var currentUserService = Substitute.For<ICurrentUserService>();
        var userAuthorizationService = Substitute.For<IUserAuthorizationService>();
        var projectMemberRepository = Substitute.For<IProjectMemberRepository>();

        userAuthorizationService.HasGlobalProjectAccessAsync(Arg.Any<CancellationToken>()).Returns(false);
        userAuthorizationService.GetCurrentUserAsync(Arg.Any<CancellationToken>())
            .Returns(new User { Email = Username, Name = Username, Role = UserRole.Viewer });
        projectMemberRepository.GetAsync(Username, ProjectName, Arg.Any<CancellationToken>())
            .Returns(new ProjectMember { Username = Username, ProjectId = ProjectName, Role = ProjectRole.Viewer });

        var service = new ProjectAccessService(currentUserService, userAuthorizationService, projectMemberRepository);

        var result = await service.HasEditAccessAsync(ProjectName);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task HasAdminAccessAsync_NonProjectMember_ReturnsFalse()
    {
        var currentUserService = Substitute.For<ICurrentUserService>();
        var userAuthorizationService = Substitute.For<IUserAuthorizationService>();
        var projectMemberRepository = Substitute.For<IProjectMemberRepository>();

        userAuthorizationService.HasGlobalProjectAccessAsync(Arg.Any<CancellationToken>()).Returns(false);
        userAuthorizationService.GetCurrentUserAsync(Arg.Any<CancellationToken>())
            .Returns(new User { Email = Username, Name = Username, Role = UserRole.Viewer });
        projectMemberRepository.GetAsync(Username, ProjectName, Arg.Any<CancellationToken>())
            .Returns((ProjectMember?)null);

        var service = new ProjectAccessService(currentUserService, userAuthorizationService, projectMemberRepository);

        var result = await service.HasEditAccessAsync(ProjectName);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task HasAdminAccessAsync_NoUsername_ReturnsFalse()
    {
        var currentUserService = Substitute.For<ICurrentUserService>();
        var userAuthorizationService = Substitute.For<IUserAuthorizationService>();
        var projectMemberRepository = Substitute.For<IProjectMemberRepository>();

        userAuthorizationService.HasGlobalProjectAccessAsync(Arg.Any<CancellationToken>()).Returns(false);
        userAuthorizationService.GetCurrentUserAsync(Arg.Any<CancellationToken>()).Returns((User?)null);
        currentUserService.Username.Returns((string?)null);

        var service = new ProjectAccessService(currentUserService, userAuthorizationService, projectMemberRepository);

        var result = await service.HasEditAccessAsync(ProjectName);

        await Assert.That(result).IsFalse();
    }
}
