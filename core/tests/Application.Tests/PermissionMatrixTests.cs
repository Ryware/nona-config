using Nona.Application.Admin.ConfigEntries.Commands;
using Nona.Application.Admin.ConfigEntries.Queries;
using Nona.Application.Admin.Environments.Commands;
using Nona.Application.Admin.Environments.Queries;
using Nona.Application.Admin.Projects.Commands;
using Nona.Application.Admin.Projects.Queries;
using Nona.Application.Tests.Common;
using Nona.Domain.Entities;
using NSubstitute;

namespace Nona.Application.Tests;

public class PermissionMatrixTests
{
    private const string ProjectName = "test-project";
    private const string EnvironmentName = "development";
    private const string ConfigKey = "test-key";
    private const string ConfigValue = "test-value";

    #region System Admin Tests

    [Test]
    [Category("SystemAdmin")]
    public async Task SystemAdmin_HasFullCrudAccess_Projects()
    {
        var fixture = new TestFixture();
        fixture.SetupAsSystemAdmin();
        fixture.SetupProjectExists(ProjectName, exists: false);
        fixture.EnvironmentRepository.ListByProjectAsync(ProjectName, Arg.Any<CancellationToken>())
            .Returns(new List<ProjectEnvironment>());
        fixture.ConfigEntryRepository.ListByProjectAsync(ProjectName, Arg.Any<CancellationToken>())
            .Returns(new List<ConfigEntry>());
        fixture.ProjectRepository.ListAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Project> { new() { Name = ProjectName } });

        // Create
        var createHandler = new CreateProjectCommandHandler(
            fixture.ProjectRepository,
            fixture.UserAuthorizationService,
            fixture.EnvironmentRepository,
            fixture.Configuration,
            fixture.DateTime);
        var createResult = await createHandler.Handle(new CreateProjectCommand(ProjectName), CancellationToken.None);
        await Assert.That(createResult.Success).IsTrue();

        // Read
        var listHandler = new ListProjectsQueryHandler(fixture.ProjectRepository, fixture.ProjectMemberRepository, fixture.UserAuthorizationService);
        var listResult = await listHandler.Handle(new ListProjectsQuery(), CancellationToken.None);
        await Assert.That(listResult.Count).IsGreaterThan(0);

        // Delete
        fixture.SetupProjectExists(ProjectName, exists: true);
        var deleteHandler = new DeleteProjectCommandHandler(fixture.ProjectRepository, fixture.EnvironmentRepository, fixture.ConfigEntryRepository, fixture.ProjectMemberRepository, fixture.UserAuthorizationService);
        var deleteResult = await deleteHandler.Handle(new DeleteProjectCommand(ProjectName), CancellationToken.None);
        await Assert.That(deleteResult.Success).IsTrue();
    }

    [Test]
    [Category("SystemAdmin")]
    public async Task SystemAdmin_HasFullCrudAccess_Environments()
    {
        var fixture = new TestFixture();
        fixture.SetupAsSystemAdmin();
        fixture.SetupProjectExists(ProjectName);
        fixture.SetupEnvironmentExists(ProjectName, EnvironmentName, exists: false);
        fixture.EnvironmentRepository.ListByProjectAsync(ProjectName, Arg.Any<CancellationToken>())
            .Returns(new List<ProjectEnvironment> { new() { Name = EnvironmentName, Project = ProjectName } });
        fixture.ConfigEntryRepository.ListAsync(ProjectName, EnvironmentName, Arg.Any<CancellationToken>())
            .Returns(new List<ConfigEntry>());

        // Create
        var createHandler = new CreateEnvironmentCommandHandler(fixture.ProjectRepository, fixture.EnvironmentRepository, fixture.ProjectAccessService, fixture.DateTime);
        var createResult = await createHandler.Handle(new CreateEnvironmentCommand(ProjectName, EnvironmentName), CancellationToken.None);
        await Assert.That(createResult.Success).IsTrue();

        // Read
        var listHandler = new ListEnvironmentsQueryHandler(fixture.ProjectRepository, fixture.EnvironmentRepository, fixture.ProjectAccessService);
        var listResult = await listHandler.Handle(new ListEnvironmentsQuery(ProjectName), CancellationToken.None);
        await Assert.That(listResult.Success).IsTrue();

        // Delete
        fixture.SetupEnvironmentExists(ProjectName, EnvironmentName, exists: true);
        var deleteHandler = new DeleteEnvironmentCommandHandler(fixture.ProjectRepository, fixture.EnvironmentRepository, fixture.ConfigEntryRepository, fixture.ProjectAccessService);
        var deleteResult = await deleteHandler.Handle(new DeleteEnvironmentCommand(ProjectName, EnvironmentName), CancellationToken.None);
        await Assert.That(deleteResult.Success).IsTrue();
    }

    [Test]
    [Category("SystemAdmin")]
    public async Task SystemAdmin_HasFullCrudAccess_ConfigEntries()
    {
        var fixture = new TestFixture();
        fixture.SetupAsSystemAdmin();
        fixture.SetupProjectExists(ProjectName);
        fixture.SetupEnvironmentExists(ProjectName, EnvironmentName);
        fixture.SetupConfigEntryExists(ProjectName, EnvironmentName, ConfigKey, exists: false);

        // Create/Update
        var upsertHandler = new UpsertConfigEntryCommandHandler(fixture.ProjectRepository, fixture.EnvironmentRepository, fixture.ConfigEntryRepository, fixture.ProjectAccessService, fixture.DateTime);
        var upsertResult = await upsertHandler.Handle(new UpsertConfigEntryCommand(ProjectName, EnvironmentName, ConfigKey, ConfigValue, null, null), CancellationToken.None);
        await Assert.That(upsertResult.Success).IsTrue();

        // Read
        fixture.SetupConfigEntryExists(ProjectName, EnvironmentName, ConfigKey);
        var getHandler = new GetConfigEntryQueryHandler(fixture.ProjectRepository, fixture.EnvironmentRepository, fixture.ConfigEntryRepository, fixture.ProjectAccessService);
        var getResult = await getHandler.Handle(new GetConfigEntryQuery(ProjectName, EnvironmentName, ConfigKey), CancellationToken.None);
        await Assert.That(getResult.Success).IsTrue();

        // Delete
        var deleteHandler = new DeleteConfigEntryCommandHandler(fixture.ProjectRepository, fixture.EnvironmentRepository, fixture.ConfigEntryRepository, fixture.ProjectAccessService);
        var deleteResult = await deleteHandler.Handle(new DeleteConfigEntryCommand(ProjectName, EnvironmentName, ConfigKey), CancellationToken.None);
        await Assert.That(deleteResult.Success).IsTrue();
    }

    #endregion

    #region Project Admin Tests

    [Test]
    [Category("ProjectAdmin")]
    public async Task ProjectAdmin_CannotCreateOrDeleteProjects()
    {
        var fixture = new TestFixture();
        fixture.SetupAsProjectAdmin("projectadmin", ProjectName);
        fixture.SetupProjectExists(ProjectName, exists: false);

        // Create - should fail
        var createHandler = new CreateProjectCommandHandler(
            fixture.ProjectRepository,
            fixture.UserAuthorizationService,
            fixture.EnvironmentRepository,
            fixture.Configuration,
            fixture.DateTime);
        var createResult = await createHandler.Handle(new CreateProjectCommand(ProjectName), CancellationToken.None);
        await Assert.That(createResult.Success).IsFalse();

        // Delete - should fail
        fixture.SetupProjectExists(ProjectName, exists: true);
        var deleteHandler = new DeleteProjectCommandHandler(fixture.ProjectRepository, fixture.EnvironmentRepository, fixture.ConfigEntryRepository, fixture.ProjectMemberRepository, fixture.UserAuthorizationService);
        var deleteResult = await deleteHandler.Handle(new DeleteProjectCommand(ProjectName), CancellationToken.None);
        await Assert.That(deleteResult.Success).IsFalse();
    }

    [Test]
    [Category("ProjectAdmin")]
    public async Task ProjectAdmin_HasFullCrudAccess_Environments()
    {
        var fixture = new TestFixture();
        fixture.SetupAsProjectAdmin("projectadmin", ProjectName);
        fixture.SetupProjectExists(ProjectName);
        fixture.SetupEnvironmentExists(ProjectName, EnvironmentName, exists: false);
        fixture.EnvironmentRepository.ListByProjectAsync(ProjectName, Arg.Any<CancellationToken>())
            .Returns(new List<ProjectEnvironment> { new() { Name = EnvironmentName, Project = ProjectName } });
        fixture.ConfigEntryRepository.ListAsync(ProjectName, EnvironmentName, Arg.Any<CancellationToken>())
            .Returns(new List<ConfigEntry>());

        // Create
        var createHandler = new CreateEnvironmentCommandHandler(fixture.ProjectRepository, fixture.EnvironmentRepository, fixture.ProjectAccessService, fixture.DateTime);
        var createResult = await createHandler.Handle(new CreateEnvironmentCommand(ProjectName, EnvironmentName), CancellationToken.None);
        await Assert.That(createResult.Success).IsTrue();

        // Read
        var listHandler = new ListEnvironmentsQueryHandler(fixture.ProjectRepository, fixture.EnvironmentRepository, fixture.ProjectAccessService);
        var listResult = await listHandler.Handle(new ListEnvironmentsQuery(ProjectName), CancellationToken.None);
        await Assert.That(listResult.Success).IsTrue();

        // Delete
        fixture.SetupEnvironmentExists(ProjectName, EnvironmentName, exists: true);
        var deleteHandler = new DeleteEnvironmentCommandHandler(fixture.ProjectRepository, fixture.EnvironmentRepository, fixture.ConfigEntryRepository, fixture.ProjectAccessService);
        var deleteResult = await deleteHandler.Handle(new DeleteEnvironmentCommand(ProjectName, EnvironmentName), CancellationToken.None);
        await Assert.That(deleteResult.Success).IsTrue();
    }

    [Test]
    [Category("ProjectAdmin")]
    public async Task ProjectAdmin_HasFullCrudAccess_ConfigEntries()
    {
        var fixture = new TestFixture();
        fixture.SetupAsProjectAdmin("projectadmin", ProjectName);
        fixture.SetupProjectExists(ProjectName);
        fixture.SetupEnvironmentExists(ProjectName, EnvironmentName);
        fixture.SetupConfigEntryExists(ProjectName, EnvironmentName, ConfigKey, exists: false);

        // Create/Update
        var upsertHandler = new UpsertConfigEntryCommandHandler(fixture.ProjectRepository, fixture.EnvironmentRepository, fixture.ConfigEntryRepository, fixture.ProjectAccessService, fixture.DateTime);
        var upsertResult = await upsertHandler.Handle(new UpsertConfigEntryCommand(ProjectName, EnvironmentName, ConfigKey, ConfigValue, null, null), CancellationToken.None);
        await Assert.That(upsertResult.Success).IsTrue();

        // Read
        fixture.SetupConfigEntryExists(ProjectName, EnvironmentName, ConfigKey);
        var getHandler = new GetConfigEntryQueryHandler(fixture.ProjectRepository, fixture.EnvironmentRepository, fixture.ConfigEntryRepository, fixture.ProjectAccessService);
        var getResult = await getHandler.Handle(new GetConfigEntryQuery(ProjectName, EnvironmentName, ConfigKey), CancellationToken.None);
        await Assert.That(getResult.Success).IsTrue();

        // Delete
        var deleteHandler = new DeleteConfigEntryCommandHandler(fixture.ProjectRepository, fixture.EnvironmentRepository, fixture.ConfigEntryRepository, fixture.ProjectAccessService);
        var deleteResult = await deleteHandler.Handle(new DeleteConfigEntryCommand(ProjectName, EnvironmentName, ConfigKey), CancellationToken.None);
        await Assert.That(deleteResult.Success).IsTrue();
    }

    #endregion

    #region Project User Tests

    [Test]
    [Category("ProjectUser")]
    public async Task ProjectUser_CannotCreateOrDeleteProjects()
    {
        var fixture = new TestFixture();
        fixture.SetupAsProjectUser("regularuser", ProjectName);
        fixture.SetupProjectExists(ProjectName, exists: false);

        // Create - should fail
        var createHandler = new CreateProjectCommandHandler(
            fixture.ProjectRepository,
            fixture.UserAuthorizationService,
            fixture.EnvironmentRepository,
            fixture.Configuration,
            fixture.DateTime);
        var createResult = await createHandler.Handle(new CreateProjectCommand(ProjectName), CancellationToken.None);
        await Assert.That(createResult.Success).IsFalse();

        // Delete - should fail
        fixture.SetupProjectExists(ProjectName, exists: true);
        var deleteHandler = new DeleteProjectCommandHandler(fixture.ProjectRepository, fixture.EnvironmentRepository, fixture.ConfigEntryRepository, fixture.ProjectMemberRepository, fixture.UserAuthorizationService);
        var deleteResult = await deleteHandler.Handle(new DeleteProjectCommand(ProjectName), CancellationToken.None);
        await Assert.That(deleteResult.Success).IsFalse();
    }

    [Test]
    [Category("ProjectUser")]
    public async Task ProjectUser_CannotCreateOrDeleteEnvironments()
    {
        var fixture = new TestFixture();
        fixture.SetupAsProjectUser("regularuser", ProjectName);
        fixture.SetupProjectExists(ProjectName);
        fixture.SetupEnvironmentExists(ProjectName, EnvironmentName, exists: false);

        // Create - should fail
        var createHandler = new CreateEnvironmentCommandHandler(fixture.ProjectRepository, fixture.EnvironmentRepository, fixture.ProjectAccessService, fixture.DateTime);
        var createResult = await createHandler.Handle(new CreateEnvironmentCommand(ProjectName, EnvironmentName), CancellationToken.None);
        await Assert.That(createResult.Success).IsFalse();
        await Assert.That(createResult.Error).IsEqualTo("Access denied");

        // Delete - should fail
        fixture.SetupEnvironmentExists(ProjectName, EnvironmentName, exists: true);
        var deleteHandler = new DeleteEnvironmentCommandHandler(fixture.ProjectRepository, fixture.EnvironmentRepository, fixture.ConfigEntryRepository, fixture.ProjectAccessService);
        var deleteResult = await deleteHandler.Handle(new DeleteEnvironmentCommand(ProjectName, EnvironmentName), CancellationToken.None);
        await Assert.That(deleteResult.Success).IsFalse();
        await Assert.That(deleteResult.Error).IsEqualTo("Access denied");
    }

    [Test]
    [Category("ProjectUser")]
    public async Task ProjectUser_CanReadEnvironments()
    {
        var fixture = new TestFixture();
        fixture.SetupAsProjectUser("regularuser", ProjectName);
        fixture.SetupProjectExists(ProjectName);
        fixture.EnvironmentRepository.ListByProjectAsync(ProjectName, Arg.Any<CancellationToken>())
            .Returns(new List<ProjectEnvironment> { new() { Name = EnvironmentName, Project = ProjectName } });

        var listHandler = new ListEnvironmentsQueryHandler(fixture.ProjectRepository, fixture.EnvironmentRepository, fixture.ProjectAccessService);
        var listResult = await listHandler.Handle(new ListEnvironmentsQuery(ProjectName), CancellationToken.None);
        await Assert.That(listResult.Success).IsTrue();
    }

    [Test]
    [Category("ProjectUser")]
    public async Task ProjectUser_CanReadConfigEntries_ButNotUpdateOrDelete()
    {
        var fixture = new TestFixture();
        fixture.SetupAsProjectUser("regularuser", ProjectName);
        fixture.SetupProjectExists(ProjectName);
        fixture.SetupEnvironmentExists(ProjectName, EnvironmentName);
        fixture.SetupConfigEntryExists(ProjectName, EnvironmentName, ConfigKey);

        // Read - should succeed
        var getHandler = new GetConfigEntryQueryHandler(fixture.ProjectRepository, fixture.EnvironmentRepository, fixture.ConfigEntryRepository, fixture.ProjectAccessService);
        var getResult = await getHandler.Handle(new GetConfigEntryQuery(ProjectName, EnvironmentName, ConfigKey), CancellationToken.None);
        await Assert.That(getResult.Success).IsTrue();

        // Update - should fail
        var upsertHandler = new UpsertConfigEntryCommandHandler(fixture.ProjectRepository, fixture.EnvironmentRepository, fixture.ConfigEntryRepository, fixture.ProjectAccessService, fixture.DateTime);
        var upsertResult = await upsertHandler.Handle(new UpsertConfigEntryCommand(ProjectName, EnvironmentName, ConfigKey, "new-value", null, null), CancellationToken.None);
        await Assert.That(upsertResult.Success).IsFalse();
        await Assert.That(upsertResult.Error).IsEqualTo("Access denied");

        // Delete - should fail
        var deleteHandler = new DeleteConfigEntryCommandHandler(fixture.ProjectRepository, fixture.EnvironmentRepository, fixture.ConfigEntryRepository, fixture.ProjectAccessService);
        var deleteResult = await deleteHandler.Handle(new DeleteConfigEntryCommand(ProjectName, EnvironmentName, ConfigKey), CancellationToken.None);
        await Assert.That(deleteResult.Success).IsFalse();
        await Assert.That(deleteResult.Error).IsEqualTo("Access denied");
    }

    #endregion

    #region User With No Access Tests

    [Test]
    [Category("NoAccess")]
    public async Task UserWithNoAccess_CannotAccessAnything()
    {
        var fixture = new TestFixture();
        fixture.SetupAsUserWithNoProjectAccess("unauthorized", ProjectName);
        fixture.SetupProjectExists(ProjectName);
        fixture.SetupEnvironmentExists(ProjectName, EnvironmentName);
        fixture.SetupConfigEntryExists(ProjectName, EnvironmentName, ConfigKey);

        // Environments - List should fail
        var listEnvHandler = new ListEnvironmentsQueryHandler(fixture.ProjectRepository, fixture.EnvironmentRepository, fixture.ProjectAccessService);
        var listEnvResult = await listEnvHandler.Handle(new ListEnvironmentsQuery(ProjectName), CancellationToken.None);
        await Assert.That(listEnvResult.Success).IsFalse();

        // Config Entries - Get should fail
        var getHandler = new GetConfigEntryQueryHandler(fixture.ProjectRepository, fixture.EnvironmentRepository, fixture.ConfigEntryRepository, fixture.ProjectAccessService);
        var getResult = await getHandler.Handle(new GetConfigEntryQuery(ProjectName, EnvironmentName, ConfigKey), CancellationToken.None);
        await Assert.That(getResult.Success).IsFalse();

        // Config Entries - Upsert should fail
        var upsertHandler = new UpsertConfigEntryCommandHandler(fixture.ProjectRepository, fixture.EnvironmentRepository, fixture.ConfigEntryRepository, fixture.ProjectAccessService, fixture.DateTime);
        var upsertResult = await upsertHandler.Handle(new UpsertConfigEntryCommand(ProjectName, EnvironmentName, ConfigKey, ConfigValue, null, null), CancellationToken.None);
        await Assert.That(upsertResult.Success).IsFalse();
    }

    #endregion
}
