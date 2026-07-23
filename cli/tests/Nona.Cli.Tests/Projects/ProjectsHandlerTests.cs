using System.Net;
using Nona.Cli.Projects.Commands;
using Nona.Cli.Projects.Queries;
using static Nona.Cli.Tests.Fixtures;
using static Nona.Cli.Tests.TestHelpers;

#pragma warning disable TUnit0055

namespace Nona.Cli.Tests.Projects;

[NotInParallel]
public sealed class ProjectsHandlerTests
{
    private static readonly NonaCliConnectionOptions TestConnection = new("http://nona.test", "test-token");

    [Test]
    public async Task ListProjectsQueryHandler_ReturnsZero_WithProjects()
    {
        var (result, output) = await CaptureOutputAsync(() => new ListProjectsQueryHandler(MockHttp(HttpStatusCode.OK, ProjectArrayJson))
            .HandleAsync(new ListProjectsQuery(TestConnection), CancellationToken.None));

        await Assert.That(result).IsEqualTo(0);
        await Assert.That(output).Contains("Environments: production, staging");
    }

    [Test]
    public async Task ListProjectsQueryHandler_ReturnsZero_WhenEmpty()
    {
        var result = await new ListProjectsQueryHandler(MockHttp(HttpStatusCode.OK, "[]"))
            .HandleAsync(new ListProjectsQuery(TestConnection), CancellationToken.None);
        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    public async Task ListProjectsQueryHandler_PrintsNone_WhenProjectHasNoEnvironments()
    {
        const string json = """
            [{"id":1,"name":"empty-project","urlSlug":"empty-project","environments":[],"createdAt":"2024-01-01T00:00:00Z","updatedAt":"2024-01-01T00:00:00Z"}]
            """;

        var (result, output) = await CaptureOutputAsync(() => new ListProjectsQueryHandler(MockHttp(HttpStatusCode.OK, json))
            .HandleAsync(new ListProjectsQuery(TestConnection), CancellationToken.None));

        await Assert.That(result).IsEqualTo(0);
        await Assert.That(output).Contains("Environments: (none)");
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

    private static async Task<(int Result, string Output)> CaptureOutputAsync(Func<Task<int>> action)
    {
        var previousOut = Console.Out;
        using var output = new StringWriter();

        try
        {
            Console.SetOut(output);
            var result = await action();
            return (result, output.ToString());
        }
        finally
        {
            Console.SetOut(previousOut);
        }
    }
}
