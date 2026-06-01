using System.Net;
using System.Text;

namespace Nona.Cli.Tests;

internal static class TestHelpers
{
    internal static Func<HttpClient> MockHttp(HttpStatusCode status, string json)
        => () => new HttpClient(new StaticMockHandler(status, json));

    internal sealed class TempFile : IDisposable
    {
        public string Path { get; } = System.IO.Path.GetTempFileName();
        public void Dispose() { if (File.Exists(Path)) File.Delete(Path); }
    }

    internal sealed class EnvironmentScope : IDisposable
    {
        private readonly Dictionary<string, string?> _previous = new(StringComparer.Ordinal);

        public EnvironmentScope(IReadOnlyDictionary<string, string?> values)
        {
            foreach (var (k, v) in values)
            {
                _previous[k] = Environment.GetEnvironmentVariable(k);
                Environment.SetEnvironmentVariable(k, v);
            }
        }

        public void Dispose()
        {
            foreach (var (k, v) in _previous)
                Environment.SetEnvironmentVariable(k, v);
        }
    }

    private sealed class StaticMockHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
    }
}

internal static class Fixtures
{
    internal const string ProjectJson = """
        {"id":1,"name":"my-project","urlSlug":"my-project","serverApiKey":"sk_server","clientApiKey":"ck_client","environments":["production","staging"],"createdAt":"2024-01-01T00:00:00Z","updatedAt":"2024-01-01T00:00:00Z"}
        """;

    internal const string ProjectArrayJson = $"[{ProjectJson}]";

    internal const string ApiKeyJson = """
        {"id":7,"name":"Web Client","key":"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA","project":"my-project","environment":"production","scope":"client","createdAt":"2024-01-01T00:00:00Z","updatedAt":"2024-01-01T00:00:00Z"}
        """;

    internal const string ApiKeyArrayJson = $"[{ApiKeyJson}]";

    internal const string ConfigEntryJson = """
        {"project":"my-project","environment":"production","key":"my.key","value":"my-value","contentType":"string","scope":"all","createdAt":"2024-01-01T00:00:00Z","updatedAt":"2024-01-01T00:00:00Z"}
        """;

    internal const string ConfigEntryArrayJson = $"[{ConfigEntryJson}]";

    internal const string CreateUserResponseJson = """
        {"user":{"id":1,"email":"user@example.com","name":"Test User","role":"editor","scope":"all"},"invitationToken":"inv_token_abc123"}
        """;
}
