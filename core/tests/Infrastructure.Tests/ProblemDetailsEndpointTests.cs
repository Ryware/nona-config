using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nona.Application;
using Nona.Infrastructure;
using Nona.WebApi;
using Nona.WebApi.Endpoints;

namespace Nona.Infrastructure.Tests;

public class ProblemDetailsEndpointTests
{
    private const string AttackerControlledPathMarker = "attacker-controlled-log-marker";
    private const string ExceptionHandlerCategoryName = "Nona.WebApi.Endpoints.ApiExceptionHandler";
    private const string ExceptionLogMessage = "Unhandled exception while processing an HTTP request.";

    [Test]
    [Arguments("/api/production", 401, "Unauthorized")]
    [Arguments("/admin/projects", 401, "Unauthorized")]
    public async Task AuthenticationFailures_ReturnProblemDetails(
        string path,
        int expectedStatus,
        string expectedTitle)
    {
        await using var app = await StartAppAsync();

        using var response = await app.GetTestClient().GetAsync(path);
        using var body = await ParseJsonAsync(response);

        await AssertProblemAsync(response, body.RootElement, expectedStatus, expectedTitle);
    }

    [Test]
    public async Task UnhandledException_LogsFixedMessageAndReturnsSanitizedProblemDetails()
    {
        using var loggerProvider = new CaptureLoggerProvider();
        await using var app = await StartAppAsync(loggerProvider);

        using var response = await app.GetTestClient().GetAsync($"/throw/{AttackerControlledPathMarker}");
        using var body = await ParseJsonAsync(response);

        var exceptionLogs = loggerProvider.Entries
            .Where(entry => entry.CategoryName == ExceptionHandlerCategoryName)
            .Where(entry => entry.LogLevel == LogLevel.Error)
            .ToArray();

        await Assert.That(exceptionLogs).Count().IsEqualTo(1);
        var exceptionLog = exceptionLogs[0];

        await Assert.That(exceptionLog.RenderedMessage).IsEqualTo(ExceptionLogMessage);
        await Assert.That(exceptionLog.Exception).IsNotNull();
        await Assert.That(exceptionLog.Exception).IsTypeOf<InvalidOperationException>();
        await Assert.That(exceptionLog.Exception!.Message).IsEqualTo("test exception");
        await Assert.That(exceptionLog.RenderedMessage).DoesNotContain(AttackerControlledPathMarker);
        await Assert.That(exceptionLog.RenderedState).DoesNotContain(AttackerControlledPathMarker);
        await Assert.That(exceptionLog.State.Any(property => property.Key is "Method" or "Path")).IsFalse();

        await AssertProblemAsync(response, body.RootElement, 500, "Internal Server Error");
        await Assert.That(body.RootElement.GetProperty("detail").GetString())
            .IsEqualTo("An unexpected error occurred.");
        await Assert.That(body.RootElement.GetRawText()).DoesNotContain("test exception");
    }

    private static async Task<WebApplication> StartAppAsync(ILoggerProvider? loggerProvider = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Storage:Type"] = "InMemory",
            ["Jwt:Key"] = "problem-details-tests-signing-key-1234567890",
            ["Jwt:Issuer"] = "problem-details-tests",
            ["Jwt:Audience"] = "problem-details-tests"
        });

        builder.Services.AddInfrastructureServices(builder.Configuration);
        builder.Services.AddApplicationServices(builder.Configuration);
        builder.Services.AddApiServices(builder.Configuration);
        if (loggerProvider is not null)
        {
            builder.Logging.AddProvider(loggerProvider);
        }

        var app = builder.Build();
        app.UseExceptionHandler();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapNonaEndpoints();
        app.MapGet("/throw/{marker}", ThrowForTest);

        await app.StartAsync();
        return app;
    }

    private static IResult ThrowForTest()
        => throw new InvalidOperationException("test exception");

    private sealed class CaptureLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<CapturedLogEntry> entries = new();

        public IReadOnlyCollection<CapturedLogEntry> Entries => entries.ToArray();

        public ILogger CreateLogger(string categoryName)
            => new CaptureLogger(categoryName, entries);

        public void Dispose()
        {
        }
    }

    private sealed class CaptureLogger(
        string categoryName,
        ConcurrentQueue<CapturedLogEntry> entries) : ILogger
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull
            => NoopScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var structuredState = state is IEnumerable<KeyValuePair<string, object?>> properties
                ? properties.ToArray()
                : [];

            entries.Enqueue(new CapturedLogEntry(
                categoryName,
                logLevel,
                exception,
                formatter(state, exception),
                state?.ToString() ?? string.Empty,
                structuredState));
        }
    }

    private sealed class NoopScope : IDisposable
    {
        public static NoopScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }

    private sealed record CapturedLogEntry(
        string CategoryName,
        LogLevel LogLevel,
        Exception? Exception,
        string RenderedMessage,
        string RenderedState,
        IReadOnlyList<KeyValuePair<string, object?>> State);

    private static async Task<JsonDocument> ParseJsonAsync(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }

    private static async Task AssertProblemAsync(
        HttpResponseMessage response,
        JsonElement body,
        int status,
        string title)
    {
        await Assert.That((int)response.StatusCode).IsEqualTo(status);
        await Assert.That(response.Content.Headers.ContentType?.MediaType).IsEqualTo("application/problem+json");
        await Assert.That(body.GetProperty("status").GetInt32()).IsEqualTo(status);
        await Assert.That(body.GetProperty("title").GetString()).IsEqualTo(title);
        await Assert.That(body.GetProperty("type").GetString()).Contains("rfc9110");
        await Assert.That(body.GetProperty("instance").GetString()).IsEqualTo(response.RequestMessage?.RequestUri?.AbsolutePath);
    }
}
