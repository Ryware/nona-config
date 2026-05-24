using Nona.Migrator.Core.Options;
using Nona.Migrator.Core.Services;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace Nona.Cli.Tests;

public sealed class NonaAdminClientTests
{
    [Test]
    public async Task GetProjectAsync_ResolvesProjectBySlug()
    {
        var handler = new StubHttpMessageHandler(static request =>
        {
            if (request.Method != HttpMethod.Get || request.RequestUri?.AbsoluteUri != "https://nona.example.com/admin/projects")
                throw new InvalidOperationException($"Unexpected request: {request.Method} {request.RequestUri}");

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    [
                      {
                        "id": 7,
                        "name": "mobile-app",
                        "urlSlug": "mobile",
                        "serverApiKey": "server-1",
                        "clientApiKey": "client-1",
                        "environments": ["prod", "stage"],
                        "createdAt": "2026-05-24T12:00:00Z",
                        "updatedAt": "2026-05-24T13:00:00Z"
                      }
                    ]
                    """,
                    Encoding.UTF8,
                    "application/json")
            });
        });

        using var httpClient = new HttpClient(handler);
        var client = new NonaAdminClient(httpClient, new NonaOptions
        {
            BaseUrl = "https://nona.example.com",
            ProjectName = "unused",
            BearerToken = "token-123"
        });

        var project = await client.GetProjectAsync("mobile", CancellationToken.None);

        await Assert.That(project).IsNotNull();
        await Assert.That(project!.Name).IsEqualTo("mobile-app");
        await Assert.That(project.ServerApiKey).IsEqualTo("server-1");
        await Assert.That(project.ClientApiKey).IsEqualTo("client-1");
    }

    [Test]
    public async Task RerollApiKeysAsync_SendsRequestedKeyType()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new StubHttpMessageHandler(async request =>
        {
            capturedRequest = request;
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync();

            await Assert.That(request.Method).IsEqualTo(HttpMethod.Post);
            await Assert.That(request.RequestUri!.AbsoluteUri).IsEqualTo("https://nona.example.com/admin/projects/mobile-app/reroll-keys");
            await Assert.That(body).Contains("\"keyType\": \"server\"");

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "id": 7,
                      "name": "mobile-app",
                      "urlSlug": "mobile",
                      "serverApiKey": "server-2",
                      "clientApiKey": "client-1",
                      "environments": ["prod"],
                      "createdAt": "2026-05-24T12:00:00Z",
                      "updatedAt": "2026-05-24T13:30:00Z"
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            };
        });

        using var httpClient = new HttpClient(handler);
        var client = new NonaAdminClient(httpClient, new NonaOptions
        {
            BaseUrl = "https://nona.example.com",
            ProjectName = "unused",
            BearerToken = "token-123"
        });

        var project = await client.RerollApiKeysAsync("mobile-app", "server", CancellationToken.None);

        await Assert.That(capturedRequest).IsNotNull();
        await Assert.That(capturedRequest!.Headers.Authorization).IsEqualTo(new AuthenticationHeaderValue("Bearer", "token-123"));
        await Assert.That(project.ServerApiKey).IsEqualTo("server-2");
        await Assert.That(project.ClientApiKey).IsEqualTo("client-1");
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return handler(request);
        }
    }
}
