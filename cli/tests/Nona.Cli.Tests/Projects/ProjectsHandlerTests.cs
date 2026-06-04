using System.Net;
using Nona.Cli.Projects.Commands;
using Nona.Cli.Projects.Queries;
using static Nona.Cli.Tests.Fixtures;
using static Nona.Cli.Tests.TestHelpers;

namespace Nona.Cli.Tests.Projects;

public sealed class ProjectsHandlerTests
{
    private static readonly NonaCliConnectionOptions TestConnection = new("http://nona.test", "test-token");

    [Test]
    public async Task ListProjectsQueryHandler_ReturnsZero_WithProjects()
    {
        var result = await new ListProjectsQueryHandler(MockHttp(HttpStatusCode.OK, ProjectArrayJson))
            .HandleAsync(new ListProjectsQuery(TestConnection), CancellationToken.None);
        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    public async Task ListProjectsQueryHandler_ReturnsZero_WhenEmpty()
    {
        var result = await new ListProjectsQueryHandler(MockHttp(HttpStatusCode.OK, "[]"))
            .HandleAsync(new ListProjectsQuery(TestConnection), CancellationToken.None);
        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    public async Task CreateProjectCommandHandler_ReturnsZero_OnSuccess()
    {
        var result = await new CreateProjectCommandHandler(MockHttp(HttpStatusCode.Created, ProjectJson))
            .HandleAsync(new CreateProjectCommand(TestConnection, "new-project"), CancellationToken.None);
        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    public async Task DeleteProjectCommandHandler_ReturnsZero_OnSuccess()
    {
        var result = await new DeleteProjectCommandHandler(MockHttp(HttpStatusCode.NoContent, string.Empty))
            .HandleAsync(new DeleteProjectCommand(TestConnection, "my-project"), CancellationToken.None);
        await Assert.That(result).IsEqualTo(0);
    }
}
