using Microsoft.Extensions.Configuration;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Entities;
using Nona.Domain.Interfaces;
using NSubstitute;

namespace Nona.Application.Tests.Common;

public class TestFixture
{
    public IProjectRepository ProjectRepository { get; } = Substitute.For<IProjectRepository>();
    public IEnvironmentRepository EnvironmentRepository { get; } = Substitute.For<IEnvironmentRepository>();
    public IConfigEntryRepository ConfigEntryRepository { get; } = Substitute.For<IConfigEntryRepository>();
    public IProjectMemberRepository ProjectMemberRepository { get; } = Substitute.For<IProjectMemberRepository>();
    public IUserRepository UserRepository { get; } = Substitute.For<IUserRepository>();
    public ICurrentUserService CurrentUserService { get; } = Substitute.For<ICurrentUserService>();
    public IProjectAccessService ProjectAccessService { get; } = Substitute.For<IProjectAccessService>();
    public IDateTime DateTime { get; } = Substitute.For<IDateTime>();
    public IConfiguration Configuration { get; }

    public TestFixture()
    {
        DateTime.NowUtc.Returns(System.DateTime.UtcNow);
        Configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Defaults:Environment:0"] = "Production"
            })
            .Build();
    }

    public void SetupAsSystemAdmin(string username = "admin")
    {
        CurrentUserService.Username.Returns(username);
        CurrentUserService.IsAdmin.Returns(true);
        ProjectAccessService.HasAccessAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        ProjectAccessService.HasAdminAccessAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
    }

    public void SetupAsProjectAdmin(string username, string projectName)
    {
        CurrentUserService.Username.Returns(username);
        CurrentUserService.IsAdmin.Returns(false);
        ProjectAccessService.HasAccessAsync(projectName, Arg.Any<CancellationToken>()).Returns(true);
        ProjectAccessService.HasAdminAccessAsync(projectName, Arg.Any<CancellationToken>()).Returns(true);
        ProjectMemberRepository.ExistsAsync(username, projectName, Arg.Any<CancellationToken>()).Returns(true);
        ProjectMemberRepository.GetAsync(username, projectName, Arg.Any<CancellationToken>())
            .Returns(new ProjectMember { Username = username, ProjectId = projectName, Role = ProjectRole.Editor });
    }

    public void SetupAsProjectUser(string username, string projectName)
    {
        CurrentUserService.Username.Returns(username);
        CurrentUserService.IsAdmin.Returns(false);
        ProjectAccessService.HasAccessAsync(projectName, Arg.Any<CancellationToken>()).Returns(true);
        ProjectAccessService.HasAdminAccessAsync(projectName, Arg.Any<CancellationToken>()).Returns(false);
        ProjectMemberRepository.ExistsAsync(username, projectName, Arg.Any<CancellationToken>()).Returns(true);
        ProjectMemberRepository.GetAsync(username, projectName, Arg.Any<CancellationToken>())
            .Returns(new ProjectMember { Username = username, ProjectId = projectName, Role = ProjectRole.Viewer });
    }

    public void SetupAsUserWithNoProjectAccess(string username, string projectName)
    {
        CurrentUserService.Username.Returns(username);
        CurrentUserService.IsAdmin.Returns(false);
        ProjectAccessService.HasAccessAsync(projectName, Arg.Any<CancellationToken>()).Returns(false);
        ProjectAccessService.HasAdminAccessAsync(projectName, Arg.Any<CancellationToken>()).Returns(false);
        ProjectMemberRepository.ExistsAsync(username, projectName, Arg.Any<CancellationToken>()).Returns(false);
        ProjectMemberRepository.GetAsync(username, projectName, Arg.Any<CancellationToken>()).Returns((ProjectMember?)null);
    }

    public void SetupProjectExists(string projectName, bool exists = true)
    {
        ProjectRepository.ExistsAsync(projectName, Arg.Any<CancellationToken>()).Returns(exists);
        if (exists)
        {
            ProjectRepository.GetByNameAsync(projectName, Arg.Any<CancellationToken>())
                .Returns(new Project { Name = projectName });
        }
    }

    public void SetupEnvironmentExists(string projectName, string environmentName, bool exists = true)
    {
        EnvironmentRepository.ExistsAsync(projectName, environmentName, Arg.Any<CancellationToken>()).Returns(exists);
        if (exists)
        {
            EnvironmentRepository.GetAsync(projectName, environmentName, Arg.Any<CancellationToken>())
                .Returns(new ProjectEnvironment { Name = environmentName, Project = projectName });
        }
    }

    public void SetupConfigEntryExists(string projectName, string environmentName, string key, bool exists = true)
    {
        ConfigEntryRepository.ExistsAsync(projectName, environmentName, key, Arg.Any<CancellationToken>()).Returns(exists);
        if (exists)
        {
            ConfigEntryRepository.GetAsync(projectName, environmentName, key, Arg.Any<CancellationToken>())
                .Returns(new ConfigEntry { Project = projectName, Environment = environmentName, Key = key, Value = "test-value" });
        }
    }
}
