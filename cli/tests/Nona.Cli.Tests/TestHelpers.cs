using System.Net;
using System.Text;

namespace Nona.Cli.Tests;

internal static class TestHelpers
{
    internal static Func<HttpClient> MockHttp(HttpStatusCode status, string json)
        => () => new HttpClient(new StaticMockHandler(status, json, null));

    internal static Func<HttpClient> MockHttp(HttpStatusCode status, string json, IReadOnlyDictionary<string, string> headers)
        => () => new HttpClient(new StaticMockHandler(status, json, headers));

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

    private sealed class StaticMockHandler(HttpStatusCode status, string body, IReadOnlyDictionary<string, string>? headers) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var response = new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };

            if (headers is not null)
            {
                foreach (var (name, value) in headers)
                    response.Headers.TryAddWithoutValidation(name, value);
            }

            return Task.FromResult(response);
        }
    }
}

internal static class Fixtures
{
    internal const string ProjectJson = """
        {"id":1,"name":"my-project","urlSlug":"my-project","environments":["production","staging"],"createdAt":"2024-01-01T00:00:00Z","updatedAt":"2024-01-01T00:00:00Z"}
        """;

    internal const string ProjectArrayJson = $"[{ProjectJson}]";

    internal const string ApiKeyJson = """
        {"id":7,"name":"Web Client","key":"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA","project":"my-project","environment":"production","scope":"client","createdAt":"2024-01-01T00:00:00Z","updatedAt":"2024-01-01T00:00:00Z"}
        """;

    internal const string ApiKeyArrayJson = $"[{ApiKeyJson}]";

    internal const string ConfigEntryJson = """
        {"project":"my-project","environment":"production","key":"my.key","value":"my-value","contentType":"text","scope":"all","activeVersion":1,"createdAt":"2024-01-01T00:00:00Z","updatedAt":"2024-01-01T00:00:00Z"}
        """;

    internal const string ConfigEntryArrayJson = $"[{ConfigEntryJson}]";

    internal const string ConfigEntryVersionJson = """
        {"project":"my-project","environment":"production","key":"my.key","version":1,"value":"my-value","contentType":"text","scope":"all","createdAt":"2024-01-01T00:00:00Z","actor":"alice"}
        """;

    internal const string ConfigEntryVersionArrayJson = $"[{ConfigEntryVersionJson}]";

    internal const string ParameterShareLinkJson = """
        {"id":11,"project":"my-project","environment":"production","key":"my.key","canEdit":true,"createdBy":"alice","createdAt":"2024-01-01T00:00:00Z","expiresAt":"2024-01-01T01:00:00Z","revokedAt":null}
        """;

    internal const string ParameterShareLinkArrayJson = $"[{ParameterShareLinkJson}]";

    internal const string CreatedParameterShareLinkJson = """
        {"id":11,"token":"AbCdEf1234567890","project":"my-project","environment":"production","key":"my.key","canEdit":false,"createdBy":"alice","createdAt":"2024-01-01T00:00:00Z","expiresAt":"2024-01-01T01:00:00Z","revokedAt":null}
        """;

    internal const string CreateUserResponseJson = """
        {"user":{"id":1,"email":"user@example.com","name":"Test User","role":"editor","scope":"all"},"invitationToken":"inv_token_abc123"}
        """;
}
