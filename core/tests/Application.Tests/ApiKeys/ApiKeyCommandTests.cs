using Nona.Application.Admin.ApiKeys.Commands;
using Nona.Application.Admin.ApiKeys.Queries;
using Nona.Application.Tests.Common;
using Nona.Domain.Entities;
using Nona.Domain.Enums;
using Nona.Domain.Interfaces;
using NSubstitute;

namespace Nona.Application.Tests.ApiKeys;

public class ApiKeyCommandTests
{
    private const string ProjectName = "test-project";
    private const string EnvironmentName = "production";

    [Test]
    public async Task SystemAdmin_CanCreateProjectScopedApiKey()
    {
        var fixture = new TestFixture();
        fixture.SetupAsSystemAdmin();
        SetupProject(fixture);
        fixture.ApiKeyRepository.GetByKeyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((ApiKeyAuthenticationResult?)null);

        var handler = CreateCreateHandler(fixture);

        var result = await handler.Handle(
            new CreateApiKeyCommand(ProjectName, "Mobile App", null, null),
            CancellationToken.None);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.ApiKey).IsNotNull();
        await Assert.That(result.ApiKey!.Key).Length().IsEqualTo(64);
        await Assert.That(result.ApiKey!.Project).IsEqualTo(ProjectName);
        await Assert.That(result.ApiKey!.Environment).IsNull();
        await Assert.That(result.ApiKey!.Scope).IsEqualTo("client");
        await fixture.ApiKeyRepository.Received(1).AddAsync(
            Arg.Is<ApiKey>(k =>
                k.Name == "Mobile App" &&
                k.Key.Length == 64 &&
                k.Project == ProjectName &&
                k.Environment == null &&
                k.Scope == KeyScope.Frontend),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SystemAdmin_CanCreateEnvironmentScopedApiKey()
    {
        var fixture = new TestFixture();
        fixture.SetupAsSystemAdmin();
        SetupProject(fixture);
        fixture.EnvironmentRepository.ExistsAsync(ProjectName, EnvironmentName, Arg.Any<CancellationToken>())
            .Returns(true);
        fixture.ApiKeyRepository.GetByKeyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((ApiKeyAuthenticationResult?)null);

        var handler = CreateCreateHandler(fixture);

        var result = await handler.Handle(
            new CreateApiKeyCommand(ProjectName, "Production App", EnvironmentName, "all"),
            CancellationToken.None);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.ApiKey!.Environment).IsEqualTo(EnvironmentName);
        await Assert.That(result.ApiKey!.Scope).IsEqualTo("all");
    }

    [Test]
    public async Task CreateApiKey_EnvironmentNotFound_ReturnsError()
    {
        var fixture = new TestFixture();
        fixture.SetupAsSystemAdmin();
        SetupProject(fixture);
        fixture.EnvironmentRepository.ExistsAsync(ProjectName, EnvironmentName, Arg.Any<CancellationToken>())
            .Returns(false);

        var handler = CreateCreateHandler(fixture);

        var result = await handler.Handle(
            new CreateApiKeyCommand(ProjectName, "Production App", EnvironmentName, "client"),
            CancellationToken.None);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Environment not found");
        await fixture.ApiKeyRepository.DidNotReceive().AddAsync(Arg.Any<ApiKey>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ProjectUser_CannotCreateApiKey()
    {
        var fixture = new TestFixture();
        fixture.SetupAsProjectUser("viewer", ProjectName);
        SetupProject(fixture);

        var handler = CreateCreateHandler(fixture);

        var result = await handler.Handle(
            new CreateApiKeyCommand(ProjectName, "Mobile App", null, "client"),
            CancellationToken.None);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Access denied");
        await fixture.ApiKeyRepository.DidNotReceive().AddAsync(Arg.Any<ApiKey>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task CreateApiKey_InvalidScope_ReturnsError()
    {
        var fixture = new TestFixture();
        fixture.SetupAsSystemAdmin();
        SetupProject(fixture);

        var handler = CreateCreateHandler(fixture);

        var result = await handler.Handle(
            new CreateApiKeyCommand(ProjectName, "Mobile App", null, "read-write"),
            CancellationToken.None);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("Invalid scope. Must be 'client', 'server', or 'all'.");
    }

    [Test]
    public async Task ListApiKeys_ReturnsKeysForProjectAdmins()
    {
        var fixture = new TestFixture();
        fixture.SetupAsProjectAdmin("editor", ProjectName);
        SetupProject(fixture);
        fixture.ApiKeyRepository.ListByProjectAsync(ProjectName, Arg.Any<CancellationToken>())
            .Returns(new List<ApiKey>
            {
                new()
                {
                    Id = 7,
                    Name = "Mobile App",
                    Key = new string('A', 64),
                    Project = ProjectName,
                    Environment = EnvironmentName,
                    Scope = KeyScope.Frontend
                }
            });

        var handler = new ListApiKeysQueryHandler(
            fixture.ProjectRepository,
            fixture.ApiKeyRepository,
            fixture.ProjectAccessService);

        var result = await handler.Handle(new ListApiKeysQuery(ProjectName), CancellationToken.None);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.ApiKeys.Count).IsEqualTo(1);
        await Assert.That(result.ApiKeys[0].Environment).IsEqualTo(EnvironmentName);
    }

    [Test]
    public async Task DeleteApiKey_DeletesMatchingProjectKey()
    {
        var fixture = new TestFixture();
        fixture.SetupAsSystemAdmin();
        SetupProject(fixture);
        fixture.ApiKeyRepository.GetByIdAsync(7, Arg.Any<CancellationToken>())
            .Returns(new ApiKey
            {
                Id = 7,
                Name = "Mobile App",
                Key = new string('A', 64),
                Project = ProjectName
            });

        var handler = new DeleteApiKeyCommandHandler(
            fixture.ProjectRepository,
            fixture.ApiKeyRepository,
            fixture.ProjectAccessService);

        var result = await handler.Handle(new DeleteApiKeyCommand(ProjectName, 7), CancellationToken.None);

        await Assert.That(result.Success).IsTrue();
        await fixture.ApiKeyRepository.Received(1).DeleteAsync(7, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DeleteApiKey_OtherProjectKey_ReturnsNotFound()
    {
        var fixture = new TestFixture();
        fixture.SetupAsSystemAdmin();
        SetupProject(fixture);
        fixture.ApiKeyRepository.GetByIdAsync(7, Arg.Any<CancellationToken>())
            .Returns(new ApiKey
            {
                Id = 7,
                Name = "Other App",
                Key = new string('A', 64),
                Project = "other-project"
            });

        var handler = new DeleteApiKeyCommandHandler(
            fixture.ProjectRepository,
            fixture.ApiKeyRepository,
            fixture.ProjectAccessService);

        var result = await handler.Handle(new DeleteApiKeyCommand(ProjectName, 7), CancellationToken.None);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error).IsEqualTo("API key not found");
        await fixture.ApiKeyRepository.DidNotReceive().DeleteAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    private static void SetupProject(TestFixture fixture)
    {
        fixture.ProjectRepository.GetByNameAsync(ProjectName, Arg.Any<CancellationToken>())
            .Returns(new Project { Id = 1, Name = ProjectName, UrlSlug = ProjectName });
    }

    private static CreateApiKeyCommandHandler CreateCreateHandler(TestFixture fixture)
    {
        return new CreateApiKeyCommandHandler(
            fixture.ProjectRepository,
            fixture.ApiKeyRepository,
            fixture.EnvironmentRepository,
            fixture.ProjectAccessService,
            fixture.DateTime);
    }
}
