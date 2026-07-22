using System.Net;
using System.Text;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Serialization;
using Nona.Cli.Generated.Models;
using Nona.Migrator.Core.Services;

namespace Nona.Cli.Tests.Core;

public sealed class NonaClientFactoryErrorResponseTests
{
    [Test]
    [Arguments(400, "text/plain", "value is wrong", "Bad Request", CliExitCodes.ValidationError, "Error: value is wrong (400)")]
    [Arguments(401, null, null, "Unauthorized", CliExitCodes.AuthenticationError, "Error: Unauthorized (401)")]
    [Arguments(502, "application/json", "{", "Bad Gateway", CliExitCodes.ServerError, "Error: Bad Gateway (502)")]
    [Arguments(502, "text/html", "<html>gateway failure</html>", "Bad Gateway", CliExitCodes.ServerError, "Error: Bad Gateway (502)")]
    public async Task Client_NormalizesUnexpectedErrorBodies(
        int statusCode,
        string? mediaType,
        string? body,
        string reasonPhrase,
        int expectedExitCode,
        string expectedMessage)
    {
        var transport = new StubHandler(_ => CreateResponse(
            statusCode,
            mediaType,
            body,
            reasonPhrase));
        var normalizer = new NonaApiErrorResponseHandler { InnerHandler = transport };
        var httpClient = new HttpClient(normalizer);
        using var client = NonaClientFactory.Create(
            new NonaCliConnectionOptions("https://nona.example", "token"),
            () => httpClient);

        ApiException? exception = null;
        try
        {
            await client.Admin.Projects.GetAsync();
        }
        catch (ApiException caught)
        {
            exception = caught;
        }

        await Assert.That(exception).IsNotNull();
        var error = CliExceptionHandler.Describe(exception!);
        await Assert.That(error.ExitCode).IsEqualTo(expectedExitCode);
        await Assert.That(error.Message).IsEqualTo(expectedMessage);
    }

    [Test]
    public async Task Client_PreservesValidationErrorsDuringNormalization()
    {
        const string body = """
            {
              "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
              "title": "One or more validation errors occurred.",
              "status": 400,
              "detail": "One or more validation errors occurred.",
              "instance": "/auth/register",
              "errors": {
                "Email": ["Email must be valid."],
                "Password": ["Password is too short.", "Password must contain a number."]
              }
            }
            """;
        var transport = new StubHandler(_ => CreateResponse(
            400,
            "application/problem+json",
            body,
            "Bad Request"));
        var normalizer = new NonaApiErrorResponseHandler { InnerHandler = transport };
        var httpClient = new HttpClient(normalizer);
        using var client = NonaClientFactory.Create(
            new NonaCliConnectionOptions("https://nona.example", "token"),
            () => httpClient);

        ApiValidationProblemDetails? exception = null;
        try
        {
            await client.Auth.Register.PostAsync(new RegisterCommand
            {
                Email = "invalid",
                Password = "short"
            });
        }
        catch (ApiValidationProblemDetails caught)
        {
            exception = caught;
        }

        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.Errors).IsNotNull();
        var emailErrors = ((UntypedArray)exception.Errors!.AdditionalData["Email"])
            .GetValue()
            .OfType<UntypedString>()
            .Select(value => value.GetValue()!);
        var passwordErrors = ((UntypedArray)exception.Errors.AdditionalData["Password"])
            .GetValue()
            .OfType<UntypedString>()
            .Select(value => value.GetValue()!);

        await Assert.That(emailErrors).IsEquivalentTo(["Email must be valid."]);
        await Assert.That(passwordErrors).IsEquivalentTo([
            "Password is too short.",
            "Password must contain a number."
        ]);

        var error = CliExceptionHandler.Describe(exception);
        await Assert.That(error.Message).IsEqualTo(
            "Error: One or more validation errors occurred. " +
            "Email: Email must be valid.; " +
            "Password: Password is too short.; " +
            "Password: Password must contain a number. (400)");
    }

    [Test]
    public async Task Client_DisposesInjectedTransportOnceAfterRequestFailure()
    {
        var transport = new TrackingHandler();
        var client = NonaClientFactory.Create(
            new NonaCliConnectionOptions("https://nona.example", "token"),
            () => new HttpClient(transport));
        ApiException? exception = null;

        using (client)
        {
            try
            {
                await client.Admin.Projects.GetAsync();
            }
            catch (ApiException caught)
            {
                exception = caught;
            }
        }

        client.Dispose();
        await Assert.That(exception).IsNotNull();
        await Assert.That(transport.DisposeCount).IsEqualTo(1);
    }

    private static HttpResponseMessage CreateResponse(
        int statusCode,
        string? mediaType,
        string? body,
        string reasonPhrase)
    {
        var response = new HttpResponseMessage((HttpStatusCode)statusCode)
        {
            ReasonPhrase = reasonPhrase
        };

        if (body is not null)
        {
            response.Content = mediaType is null
                ? new StringContent(body, Encoding.UTF8)
                : new StringContent(body, Encoding.UTF8, mediaType);
        }

        return response;
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(responseFactory(request));
    }

    private sealed class TrackingHandler : HttpMessageHandler
    {
        private int _disposeCount;

        internal int DisposeCount => Volatile.Read(ref _disposeCount);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(CreateResponse(
                500,
                "application/problem+json",
                "{\"title\":\"Server error\",\"status\":500}",
                "Internal Server Error"));

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                Interlocked.Increment(ref _disposeCount);

            base.Dispose(disposing);
        }
    }
}
