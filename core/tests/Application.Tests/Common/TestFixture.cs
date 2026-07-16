using Microsoft.Extensions.Configuration;
using Nona.Application.Common.Interfaces;
using Nona.Domain.Entities;
using Nona.Domain.Interfaces;
using NSubstitute;

namespace Nona.Application.Tests.Common;

public class TestFixture
{
    public IProjectRepository ProjectRepository { get; } = Substitute.For<IProjectRepository>();
    public IApiKeyRepository ApiKeyRepository { get; } = Substitute.For<IApiKeyRepository>();
    public IEnvironmentRepository EnvironmentRepository { get; } = Substitute.For<IEnvironmentRepository>();
    public IConfigEntryRepository ConfigEntryRepository { get; } = Substitute.For<IConfigEntryRepository>();
    public IConfigReleaseRepository ConfigReleaseRepository { get; } = Substitute.For<IConfigReleaseRepository>();
    public IProjectMemberRepository ProjectMemberRepository { get; } = Substitute.For<IProjectMemberRepository>();
    public IUserRepository UserRepository { get; } = Substitute.For<IUserRepository>();
    public ICurrentUserService CurrentUserService { get; } = Substitute.For<ICurrentUserService>();
    public IUserAuthorizationService UserAuthorizationService { get; } = Substitute.For<IUserAuthorizationService>();
    public IProjectAccessService ProjectAccessService { get; } = Substitute.For<IProjectAccessService>();
    public IDateTime DateTime { get; } = Substitute.For<IDateTime>();
    public IConfiguration Configuration { get; }

    public TestFixture()
    {
        DateTime.NowUtc.Returns(System.DateTime.UtcNow);
        ConfigEntryRepository.AddVersionAsync(Arg.Any<ConfigEntry>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var entry = call.ArgAt<ConfigEntry>(0);
                return new ConfigEntry
                {
                    Project = entry.Project,
                    Environment = entry.Environment,
                    Key = entry.Key,
                    Value = entry.Value,
                    ContentType = entry.ContentType,
                    Scope = entry.Scope,
                    ActiveVersion = entry.ActiveVersion,
                    CreatedAt = entry.CreatedAt,
                    UpdatedAt = entry.UpdatedAt
                };
            });
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
        CurrentUserService.Role.Returns(UserRole.Viewer);
        CurrentUserService.IsAdmin.Returns(true);
        UserAuthorizationService.GetCurrentUserAsync(Arg.Any<CancellationToken>())
            .Returns(new User { Email = username, Name = username, IsAdmin = true, Role = UserRole.Viewer });
        UserAuthorizationService.CanManageUsersAsync(Arg.Any<CancellationToken>()).Returns(true);
        UserAuthorizationService.HasGlobalProjectAccessAsync(Arg.Any<CancellationToken>()).Returns(true);
        ProjectAccessService.HasViewAccessAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        ProjectAccessService.HasEditAccessAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
    }

    public void SetupAsProjectAdmin(string username, string projectName)
    {
        CurrentUserService.Username.Returns(username);
        CurrentUserService.Role.Returns(UserRole.Viewer);
        CurrentUserService.IsAdmin.Returns(false);
        UserAuthorizationService.GetCurrentUserAsync(Arg.Any<CancellationToken>())
            .Returns(new User { Email = username, Name = username, Role = UserRole.Viewer });
        UserAuthorizationService.CanManageUsersAsync(Arg.Any<CancellationToken>()).Returns(false);
        UserAuthorizationService.HasGlobalProjectAccessAsync(Arg.Any<CancellationToken>()).Returns(false);
        ProjectAccessService.HasViewAccessAsync(projectName, Arg.Any<CancellationToken>()).Returns(true);
        ProjectAccessService.HasEditAccessAsync(projectName, Arg.Any<CancellationToken>()).Returns(true);
        ProjectMemberRepository.ExistsAsync(username, projectName, Arg.Any<CancellationToken>()).Returns(true);
        ProjectMemberRepository.GetAsync(username, projectName, Arg.Any<CancellationToken>())
            .Returns(new ProjectMember { Username = username, ProjectId = projectName, Role = ProjectRole.Editor });
    }

    public void SetupAsProjectUser(string username, string projectName)
    {
        CurrentUserService.Username.Returns(username);
        CurrentUserService.Role.Returns(UserRole.Viewer);
        CurrentUserService.IsAdmin.Returns(false);
        UserAuthorizationService.GetCurrentUserAsync(Arg.Any<CancellationToken>())
            .Returns(new User { Email = username, Name = username, Role = UserRole.Viewer });
        UserAuthorizationService.CanManageUsersAsync(Arg.Any<CancellationToken>()).Returns(false);
        UserAuthorizationService.HasGlobalProjectAccessAsync(Arg.Any<CancellationToken>()).Returns(false);
        ProjectAccessService.HasViewAccessAsync(projectName, Arg.Any<CancellationToken>()).Returns(true);
        ProjectAccessService.HasEditAccessAsync(projectName, Arg.Any<CancellationToken>()).Returns(false);
        ProjectMemberRepository.ExistsAsync(username, projectName, Arg.Any<CancellationToken>()).Returns(true);
        ProjectMemberRepository.GetAsync(username, projectName, Arg.Any<CancellationToken>())
            .Returns(new ProjectMember { Username = username, ProjectId = projectName, Role = ProjectRole.Viewer });
    }

    public void SetupAsUserWithNoProjectAccess(string username, string projectName)
    {
        CurrentUserService.Username.Returns(username);
        CurrentUserService.Role.Returns(UserRole.Viewer);
        CurrentUserService.IsAdmin.Returns(false);
        UserAuthorizationService.GetCurrentUserAsync(Arg.Any<CancellationToken>())
            .Returns(new User { Email = username, Name = username, Role = UserRole.Viewer });
        UserAuthorizationService.CanManageUsersAsync(Arg.Any<CancellationToken>()).Returns(false);
        UserAuthorizationService.HasGlobalProjectAccessAsync(Arg.Any<CancellationToken>()).Returns(false);
        ProjectAccessService.HasViewAccessAsync(projectName, Arg.Any<CancellationToken>()).Returns(false);
        ProjectAccessService.HasEditAccessAsync(projectName, Arg.Any<CancellationToken>()).Returns(false);
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
