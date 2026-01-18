using Nona.Application.Admin.Projects.Commands;
using Nona.Application.Tests.Common;
using Nona.Domain.Entities;
using NSubstitute;

namespace Nona.Application.Tests.Projects;

public class RerollApiKeysCommandTests
{
    private const string ProjectName = "test-project";

    [Test]
    public async Task SystemAdmin_CanRerollServerKey()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.SetupAsSystemAdmin();
        var project = new Project
        {
            Name = ProjectName,
            ServerApiKey = "old-server-key",
            ClientApiKey = "old-client-key"
        };
        fixture.ProjectRepository.GetByNameAsync(ProjectName, Arg.Any<CancellationToken>()).Returns(project);

        var handler = new RerollApiKeysCommandHandler(
            fixture.ProjectRepository,
            fixture.ProjectAccessService,
            fixture.DateTime);

        var command = new RerollApiKeysCommand(ProjectName, ApiKeyType.Server);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Project).IsNotNull();
        await Assert.That(result.Project!.ServerApiKey).IsNotEqualTo("old-server-key");
        await Assert.That(result.Project!.ClientApiKey).IsEqualTo("old-client-key");
        await fixture.ProjectRepository.Received(1).UpdateAsync(project, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SystemAdmin_CanRerollClientKey()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.SetupAsSystemAdmin();
        var project = new Project
        {
            Name = ProjectName,
            ServerApiKey = "old-server-key",
            ClientApiKey = "old-client-key"
        };
        fixture.ProjectRepository.GetByNameAsync(ProjectName, Arg.Any<CancellationToken>()).Returns(project);

        var handler = new RerollApiKeysCommandHandler(
            fixture.ProjectRepository,
            fixture.ProjectAccessService,
            fixture.DateTime);

        var command = new RerollApiKeysCommand(ProjectName, ApiKeyType.Client);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Project).IsNotNull();
        await Assert.That(result.Project!.ServerApiKey).IsEqualTo("old-server-key");
        await Assert.That(result.Project!.ClientApiKey).IsNotEqualTo("old-client-key");
    }

    [Test]
    public async Task SystemAdmin_CanRerollBothKeys()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.SetupAsSystemAdmin();
        var project = new Project
        {
            Name = ProjectName,
            ServerApiKey = "old-server-key",
            ClientApiKey = "old-client-key"
        };
        fixture.ProjectRepository.GetByNameAsync(ProjectName, Arg.Any<CancellationToken>()).Returns(project);

        var handler = new RerollApiKeysCommandHandler(
            fixture.ProjectRepository,
            fixture.ProjectAccessService,
            fixture.DateTime);

        var command = new RerollApiKeysCommand(ProjectName, ApiKeyType.Both);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Project).IsNotNull();
        await Assert.That(result.Project!.ServerApiKey).IsNotEqualTo("old-server-key");
        await Assert.That(result.Project!.ClientApiKey).IsNotEqualTo("old-client-key");
    }

    [Test]
    public async Task ProjectAdmin_CanRerollKeys()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.SetupAsProjectAdmin("projectadmin", ProjectName);
        var project = new Project
        {
            Name = ProjectName,
            ServerApiKey = "old-server-key",
            ClientApiKey = "old-client-key"
        };
        fixture.ProjectRepository.GetByNameAsync(ProjectName, Arg.Any<CancellationToken>()).Returns(project);

        var handler = new RerollApiKeysCommandHandler(
            fixture.ProjectRepository,
            fixture.ProjectAccessService,
            fixture.DateTime);

        var command = new RerollApiKeysCommand(ProjectName, ApiKeyType.Both);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Project).IsNotNull();
        await fixture.ProjectRepository.Received(1).UpdateAsync(project, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ProjectUser_CannotRerollKeys()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.SetupAsProjectUser("regularuser", ProjectName);
        var project = new Project
        {
            Name = ProjectName,
            ServerApiKey = "old-server-key",
            ClientApiKey = "old-client-key"
        };
        fixture.ProjectRepository.GetByNameAsync(ProjectName, Arg.Any<CancellationToken>()).Returns(project);

        var handler = new RerollApiKeysCommandHandler(
            fixture.ProjectRepository,
            fixture.ProjectAccessService,
            fixture.DateTime);

        var command = new RerollApiKeysCommand(ProjectName, ApiKeyType.Both);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Access denied");
        await fixture.ProjectRepository.DidNotReceive().UpdateAsync(Arg.Any<Project>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task UserWithNoAccess_CannotRerollKeys()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.SetupAsUserWithNoProjectAccess("unauthorized", ProjectName);
        var project = new Project
        {
            Name = ProjectName,
            ServerApiKey = "old-server-key",
            ClientApiKey = "old-client-key"
        };
        fixture.ProjectRepository.GetByNameAsync(ProjectName, Arg.Any<CancellationToken>()).Returns(project);

        var handler = new RerollApiKeysCommandHandler(
            fixture.ProjectRepository,
            fixture.ProjectAccessService,
            fixture.DateTime);

        var command = new RerollApiKeysCommand(ProjectName, ApiKeyType.Both);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Access denied");
    }

    [Test]
    public async Task RerollKeys_ProjectNotFound_ReturnsFalse()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.SetupAsSystemAdmin();
        fixture.ProjectRepository.GetByNameAsync(ProjectName, Arg.Any<CancellationToken>()).Returns((Project?)null);

        var handler = new RerollApiKeysCommandHandler(
            fixture.ProjectRepository,
            fixture.ProjectAccessService,
            fixture.DateTime);

        var command = new RerollApiKeysCommand(ProjectName, ApiKeyType.Both);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Project not found");
    }

    [Test]
    public async Task RerollKeys_GeneratesValidHexKeys()
    {
        // Arrange
        var fixture = new TestFixture();
        fixture.SetupAsSystemAdmin();
        var project = new Project
        {
            Name = ProjectName,
            ServerApiKey = "old-server-key",
            ClientApiKey = "old-client-key"
        };
        fixture.ProjectRepository.GetByNameAsync(ProjectName, Arg.Any<CancellationToken>()).Returns(project);

        var handler = new RerollApiKeysCommandHandler(
            fixture.ProjectRepository,
            fixture.ProjectAccessService,
            fixture.DateTime);

        var command = new RerollApiKeysCommand(ProjectName, ApiKeyType.Both);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Project!.ServerApiKey).Length().IsEqualTo(64); // 32 bytes = 64 hex chars
        await Assert.That(result.Project!.ClientApiKey).Length().IsEqualTo(64);
        await Assert.That(result.Project!.ServerApiKey).IsNotEqualTo(result.Project!.ClientApiKey);
    }
}
