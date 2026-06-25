using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Nona.Infrastructure.Tests.Common;
using Nona.Libsql;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Nona.Infrastructure.Tests;

[NotInParallel]
public class SsoApiTests
{
    [Test]
    public async Task SsoEndpoints_ValidateTokens_AndRejectUnknownOrMismatchedUsers()
    {
        var artifactsRoot = Path.Combine(Path.GetTempPath(), $"nona-sso-api-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(artifactsRoot);

        var success = false;
        ManagedProcess? backend = null;
        await using var signingKey = new SigningKeyFixture();
        await using var jwksServer = await LocalJwksServer.StartAsync(signingKey.CreateJwksDocument());

        var port = GetFreeTcpPort();
        var libsqlPort = GetFreeTcpPort();
        while (libsqlPort == port)
        {
            libsqlPort = GetFreeTcpPort();
        }

        var libsqlUrl = $"http://127.0.0.1:{libsqlPort}";
        var databasePath = Path.Combine(artifactsRoot, "nona-sso-api.db");

        try
        {
            await RunProcessCheckedAsync(
                "build",
                "dotnet",
                $"build \"{TestPaths.ResolveWebApiProject()}\"",
                TestPaths.ResolveRepoRoot(),
                artifactsRoot,
                TimeSpan.FromMinutes(5));

            backend = StartProcess(
                "backend",
                "dotnet",
                $"\"{TestPaths.ResolveWebApiOutputAssembly()}\"",
                TestPaths.ResolveWebApiWorkingDirectory(),
                new Dictionary<string, string?>
                {
                    ["ASPNETCORE_ENVIRONMENT"] = "Development",
                    ["ASPNETCORE_URLS"] = $"http://127.0.0.1:{port}",
                    ["Storage__Type"] = "Libsql",
                    ["Storage__Libsql__ManagedPrimary__Enabled"] = "true",
                    ["Storage__Libsql__ManagedPrimary__ExecutablePath"] = "sqld",
                    ["Storage__Libsql__ManagedPrimary__DatabasePath"] = databasePath,
                    ["Storage__Libsql__ManagedPrimary__WorkingDirectory"] = artifactsRoot,
                    ["Storage__Libsql__ManagedPrimary__HttpListenAddress"] = $"127.0.0.1:{libsqlPort}",
                    ["Storage__Libsql__ManagedPrimary__LocalConnectUrl"] = libsqlUrl,
                    ["Jwt__Key"] = "sso-api-tests-signing-key-1234567890",
                    ["Jwt__Issuer"] = "sso-api-tests",
                    ["Jwt__Audience"] = "sso-api-tests",
                    ["Sso__Google__ClientId"] = "google-client-id",
                    ["Sso__Microsoft__ClientId"] = "microsoft-client-id",
                    ["Sso__Microsoft__TenantId"] = "allowed-tenant",
                    ["Sso__Google__JwksUri"] = jwksServer.GoogleJwksUrl,
                    ["Sso__Google__Issuers__0"] = "https://accounts.google.com",
                    ["Sso__Microsoft__JwksUri"] = jwksServer.MicrosoftJwksUrl
                });

            await WaitForBackendHealthyAsync(backend, port, artifactsRoot);

            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            var baseUrl = $"http://127.0.0.1:{port}";

            using (var ssoConfig = await SendJsonAsync(httpClient, HttpMethod.Get, $"{baseUrl}/auth/sso/config"))
            {
                await Assert.That(ssoConfig.RootElement.GetProperty("google").GetProperty("enabled").GetBoolean()).IsTrue();
                await Assert.That(ssoConfig.RootElement.GetProperty("google").GetProperty("clientId").GetString()).IsEqualTo("google-client-id");
                await Assert.That(ssoConfig.RootElement.GetProperty("microsoft").GetProperty("enabled").GetBoolean()).IsTrue();
                await Assert.That(ssoConfig.RootElement.GetProperty("microsoft").GetProperty("tenantId").GetString()).IsEqualTo("allowed-tenant");
            }

            using var register = await SendJsonAsync(
                httpClient,
                HttpMethod.Post,
                $"{baseUrl}/auth/register",
                new { email = "admin@example.com", password = "Password123!" });
            await Assert.That(register.RootElement.GetProperty("success").GetBoolean()).IsTrue();
            var adminToken = register.RootElement.GetProperty("response").GetProperty("token").GetString()
                ?? throw new InvalidOperationException("Register response did not include a token.");

            using (var createUser = await SendJsonAsync(
                httpClient,
                HttpMethod.Post,
                $"{baseUrl}/admin/users",
                new { name = "Microsoft User", email = "ms@example.com" },
                adminToken))
            {
                await Assert.That(createUser.RootElement.GetProperty("user").GetProperty("email").GetString()).IsEqualTo("ms@example.com");
                await Assert.That(createUser.RootElement.GetProperty("invitationToken").GetString()).IsNotNull();
            }

            var googleToken = signingKey.CreateToken(
                issuer: "https://accounts.google.com",
                audience: "google-client-id",
                claims:
                [
                    new Claim("sub", "google-user-1"),
                    new Claim("email", "admin@example.com"),
                    new Claim("name", "Admin User"),
                    new Claim("email_verified", "true")
                ]);

            using (var googleLogin = await SendJsonAsync(
                httpClient,
                HttpMethod.Post,
                $"{baseUrl}/auth/sso/google",
                new { idToken = googleToken }))
            {
                await Assert.That(googleLogin.RootElement.GetProperty("token").GetString()).IsNotNull();
            }

            await Assert.That(await CountExternalIdentitiesAsync(libsqlUrl)).IsEqualTo(1);

            using (var googleRelogin = await SendJsonAsync(
                httpClient,
                HttpMethod.Post,
                $"{baseUrl}/auth/sso/google",
                new { idToken = googleToken }))
            {
                await Assert.That(googleRelogin.RootElement.GetProperty("token").GetString()).IsNotNull();
            }

            await Assert.That(await CountExternalIdentitiesAsync(libsqlUrl)).IsEqualTo(1);

            var microsoftToken = signingKey.CreateToken(
                issuer: "https://login.microsoftonline.com/allowed-tenant/v2.0",
                audience: "microsoft-client-id",
                claims:
                [
                    new Claim("sub", "microsoft-user-1"),
                    new Claim("tid", "allowed-tenant"),
                    new Claim("preferred_username", "ms@example.com"),
                    new Claim("name", "Microsoft User")
                ]);

            using (var microsoftLogin = await SendJsonAsync(
                httpClient,
                HttpMethod.Post,
                $"{baseUrl}/auth/sso/microsoft",
                new { idToken = microsoftToken }))
            {
                await Assert.That(microsoftLogin.RootElement.GetProperty("token").GetString()).IsNotNull();
            }

            await Assert.That(await CountExternalIdentitiesAsync(libsqlUrl)).IsEqualTo(2);

            var mismatchedGoogleToken = signingKey.CreateToken(
                issuer: "https://accounts.google.com",
                audience: "google-client-id",
                claims:
                [
                    new Claim("sub", "google-user-2"),
                    new Claim("email", "admin@example.com"),
                    new Claim("email_verified", "true")
                ]);

            using (var mismatchResponse = await SendRawAsync(
                httpClient,
                HttpMethod.Post,
                $"{baseUrl}/auth/sso/google",
                body: new { idToken = mismatchedGoogleToken }))
            {
                await Assert.That(mismatchResponse.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
                using var body = await ParseJsonAsync(mismatchResponse);
                await Assert.That(body.RootElement.GetProperty("error").GetString()).IsEqualTo("Authentication failed");
            }

            var unknownMicrosoftToken = signingKey.CreateToken(
                issuer: "https://login.microsoftonline.com/allowed-tenant/v2.0",
                audience: "microsoft-client-id",
                claims:
                [
                    new Claim("sub", "microsoft-user-2"),
                    new Claim("tid", "allowed-tenant"),
                    new Claim("preferred_username", "unknown@example.com")
                ]);

            using (var unknownResponse = await SendRawAsync(
                httpClient,
                HttpMethod.Post,
                $"{baseUrl}/auth/sso/microsoft",
                body: new { idToken = unknownMicrosoftToken }))
            {
                await Assert.That(unknownResponse.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
                using var body = await ParseJsonAsync(unknownResponse);
                await Assert.That(body.RootElement.GetProperty("error").GetString()).IsEqualTo("Authentication failed");
                await Assert.That(body.RootElement.GetProperty("errorCode").GetString()).IsEqualTo("sso_user_not_registered");
            }

            using (var invalidResponse = await SendRawAsync(
                httpClient,
                HttpMethod.Post,
                $"{baseUrl}/auth/sso/google",
                body: new { idToken = googleToken[..^1] + "x" }))
            {
                await Assert.That(invalidResponse.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
            }

            success = true;
        }
        finally
        {
            if (backend is not null)
            {
                await backend.DisposeAsync();
            }

            if (!success)
            {
                Console.WriteLine($"SSO API test artifacts kept at: {artifactsRoot}");
            }
            else
            {
                try
                {
                    Directory.Delete(artifactsRoot, recursive: true);
                }
                catch
                {
                }
            }
        }
    }

    [Test]
    public async Task InvitationEndpoints_CompleteOnce_AndSupportPasswordOrSso()
    {
        var artifactsRoot = Path.Combine(Path.GetTempPath(), $"nona-invite-api-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(artifactsRoot);

        var success = false;
        ManagedProcess? backend = null;
        await using var signingKey = new SigningKeyFixture();
        await using var jwksServer = await LocalJwksServer.StartAsync(signingKey.CreateJwksDocument());

        var port = GetFreeTcpPort();
        var libsqlPort = GetFreeTcpPort();
        while (libsqlPort == port)
        {
            libsqlPort = GetFreeTcpPort();
        }

        var libsqlUrl = $"http://127.0.0.1:{libsqlPort}";
        var databasePath = Path.Combine(artifactsRoot, "nona-invite-api.db");

        try
        {
            await RunProcessCheckedAsync(
                "build",
                "dotnet",
                $"build \"{TestPaths.ResolveWebApiProject()}\"",
                TestPaths.ResolveRepoRoot(),
                artifactsRoot,
                TimeSpan.FromMinutes(5));

            backend = StartProcess(
                "backend",
                "dotnet",
                $"\"{TestPaths.ResolveWebApiOutputAssembly()}\"",
                TestPaths.ResolveWebApiWorkingDirectory(),
                new Dictionary<string, string?>
                {
                    ["ASPNETCORE_ENVIRONMENT"] = "Development",
                    ["ASPNETCORE_URLS"] = $"http://127.0.0.1:{port}",
                    ["Storage__Type"] = "Libsql",
                    ["Storage__Libsql__ManagedPrimary__Enabled"] = "true",
                    ["Storage__Libsql__ManagedPrimary__ExecutablePath"] = "sqld",
                    ["Storage__Libsql__ManagedPrimary__DatabasePath"] = databasePath,
                    ["Storage__Libsql__ManagedPrimary__WorkingDirectory"] = artifactsRoot,
                    ["Storage__Libsql__ManagedPrimary__HttpListenAddress"] = $"127.0.0.1:{libsqlPort}",
                    ["Storage__Libsql__ManagedPrimary__LocalConnectUrl"] = libsqlUrl,
                    ["Jwt__Key"] = "invite-api-tests-signing-key-1234567890",
                    ["Jwt__Issuer"] = "invite-api-tests",
                    ["Jwt__Audience"] = "invite-api-tests",
                    ["Sso__Google__ClientId"] = "google-client-id",
                    ["Sso__Microsoft__ClientId"] = "microsoft-client-id",
                    ["Sso__Microsoft__TenantId"] = "allowed-tenant",
                    ["Sso__Google__JwksUri"] = jwksServer.GoogleJwksUrl,
                    ["Sso__Google__Issuers__0"] = "https://accounts.google.com",
                    ["Sso__Microsoft__JwksUri"] = jwksServer.MicrosoftJwksUrl
                });

            await WaitForBackendHealthyAsync(backend, port, artifactsRoot);

            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            var baseUrl = $"http://127.0.0.1:{port}";

            using var register = await SendJsonAsync(
                httpClient,
                HttpMethod.Post,
                $"{baseUrl}/auth/register",
                new { email = "admin@example.com", password = "Password123!" });
            var adminToken = register.RootElement.GetProperty("response").GetProperty("token").GetString()
                ?? throw new InvalidOperationException("Register response did not include a token.");

            string passwordInviteToken;
            using (var createUser = await SendJsonAsync(
                httpClient,
                HttpMethod.Post,
                $"{baseUrl}/admin/users",
                new { name = "Password Invitee", email = "password-invite@example.com" },
                adminToken))
            {
                passwordInviteToken = createUser.RootElement.GetProperty("invitationToken").GetString()
                    ?? throw new InvalidOperationException("Create user response did not include an invitation token.");
            }

            using (var invitation = await SendJsonAsync(
                httpClient,
                HttpMethod.Get,
                $"{baseUrl}/auth/invitations/{passwordInviteToken}"))
            {
                await Assert.That(invitation.RootElement.GetProperty("email").GetString()).IsEqualTo("password-invite@example.com");
            }

            var mismatchedGoogleToken = signingKey.CreateToken(
                issuer: "https://accounts.google.com",
                audience: "google-client-id",
                claims:
                [
                    new Claim("sub", "google-mismatch"),
                    new Claim("email", "wrong@example.com"),
                    new Claim("name", "Wrong User"),
                    new Claim("email_verified", "true")
                ]);

            using (var mismatchResponse = await SendRawAsync(
                httpClient,
                HttpMethod.Post,
                $"{baseUrl}/auth/invitations/{passwordInviteToken}/sso/google",
                body: new { idToken = mismatchedGoogleToken }))
            {
                await Assert.That(mismatchResponse.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
                using var body = await ParseJsonAsync(mismatchResponse);
                await Assert.That(body.RootElement.GetProperty("errorCode").GetString()).IsEqualTo("invitation_sso_email_mismatch");
            }

            using (var passwordComplete = await SendJsonAsync(
                httpClient,
                HttpMethod.Post,
                $"{baseUrl}/auth/invitations/{passwordInviteToken}/password",
                new { newPassword = "Password123!" }))
            {
                await Assert.That(passwordComplete.RootElement.GetProperty("token").GetString()).IsNotNull();
            }

            using (var consumedInvite = await SendRawAsync(
                httpClient,
                HttpMethod.Get,
                $"{baseUrl}/auth/invitations/{passwordInviteToken}"))
            {
                await Assert.That(consumedInvite.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
                using var body = await ParseJsonAsync(consumedInvite);
                await Assert.That(body.RootElement.GetProperty("errorCode").GetString()).IsEqualTo("invitation_invalid_or_used");
            }

            using (var passwordLogin = await SendJsonAsync(
                httpClient,
                HttpMethod.Post,
                $"{baseUrl}/auth/login",
                new { email = "password-invite@example.com", password = "Password123!" }))
            {
                await Assert.That(passwordLogin.RootElement.GetProperty("token").GetString()).IsNotNull();
            }

            string ssoInviteToken;
            using (var createUser = await SendJsonAsync(
                httpClient,
                HttpMethod.Post,
                $"{baseUrl}/admin/users",
                new { name = "SSO Invitee", email = "sso-invite@example.com" },
                adminToken))
            {
                ssoInviteToken = createUser.RootElement.GetProperty("invitationToken").GetString()
                    ?? throw new InvalidOperationException("Create user response did not include an invitation token.");
            }

            var matchingGoogleToken = signingKey.CreateToken(
                issuer: "https://accounts.google.com",
                audience: "google-client-id",
                claims:
                [
                    new Claim("sub", "google-sso-invite"),
                    new Claim("email", "sso-invite@example.com"),
                    new Claim("name", "SSO Invitee"),
                    new Claim("email_verified", "true")
                ]);

            using (var ssoComplete = await SendJsonAsync(
                httpClient,
                HttpMethod.Post,
                $"{baseUrl}/auth/invitations/{ssoInviteToken}/sso/google",
                new { idToken = matchingGoogleToken }))
            {
                await Assert.That(ssoComplete.RootElement.GetProperty("token").GetString()).IsNotNull();
            }

            using (var consumedInvite = await SendRawAsync(
                httpClient,
                HttpMethod.Get,
                $"{baseUrl}/auth/invitations/{ssoInviteToken}"))
            {
                await Assert.That(consumedInvite.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
            }

            using (var passwordFailure = await SendRawAsync(
                httpClient,
                HttpMethod.Post,
                $"{baseUrl}/auth/login",
                body: new { email = "sso-invite@example.com", password = "Password123!" }))
            {
                await Assert.That(passwordFailure.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
            }

            string normalSsoInviteToken;
            using (var createUser = await SendJsonAsync(
                httpClient,
                HttpMethod.Post,
                $"{baseUrl}/admin/users",
                new { name = "Normal SSO Invitee", email = "normal-sso@example.com" },
                adminToken))
            {
                normalSsoInviteToken = createUser.RootElement.GetProperty("invitationToken").GetString()
                    ?? throw new InvalidOperationException("Create user response did not include an invitation token.");
            }

            var normalSsoToken = signingKey.CreateToken(
                issuer: "https://accounts.google.com",
                audience: "google-client-id",
                claims:
                [
                    new Claim("sub", "google-normal-sso"),
                    new Claim("email", "normal-sso@example.com"),
                    new Claim("name", "Normal SSO Invitee"),
                    new Claim("email_verified", "true")
                ]);

            using (var normalSsoLogin = await SendJsonAsync(
                httpClient,
                HttpMethod.Post,
                $"{baseUrl}/auth/sso/google",
                new { idToken = normalSsoToken }))
            {
                await Assert.That(normalSsoLogin.RootElement.GetProperty("token").GetString()).IsNotNull();
            }

            using (var consumedInvite = await SendRawAsync(
                httpClient,
                HttpMethod.Get,
                $"{baseUrl}/auth/invitations/{normalSsoInviteToken}"))
            {
                await Assert.That(consumedInvite.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
            }

            success = true;
        }
        finally
        {
            if (backend is not null)
            {
                await backend.DisposeAsync();
            }

            if (!success)
            {
                Console.WriteLine($"Invite API test artifacts kept at: {artifactsRoot}");
            }
            else
            {
                try
                {
                    Directory.Delete(artifactsRoot, recursive: true);
                }
                catch
                {
                }
            }
        }
    }

    private static async Task WaitForBackendHealthyAsync(ManagedProcess process, int port, string artifactsRoot)
    {
        using var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5)
        };

        var healthUrl = $"http://127.0.0.1:{port}/auth/first-time";
        var deadline = DateTime.UtcNow + TimeSpan.FromMinutes(1);

        while (DateTime.UtcNow < deadline)
        {
            if (process.Process.HasExited)
            {
                var (stdout, stderr) = await process.GetOutputAsync();
                await process.WriteLogsAsync(artifactsRoot);

                throw new InvalidOperationException(
                    $"Backend exited before becoming healthy.{Environment.NewLine}" +
                    $"stdout:{Environment.NewLine}{stdout}{Environment.NewLine}" +
                    $"stderr:{Environment.NewLine}{stderr}");
            }

            try
            {
                using var response = await httpClient.GetAsync(healthUrl);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch
            {
            }

            await Task.Delay(250);
        }

        await process.WriteLogsAsync(artifactsRoot);
        throw new TimeoutException($"Timed out waiting for backend at {healthUrl}.");
    }

    private static async Task RunProcessCheckedAsync(
        string name,
        string fileName,
        string arguments,
        string workingDirectory,
        string artifactsRoot,
        TimeSpan timeout)
    {
        var process = StartProcess(name, fileName, arguments, workingDirectory, new Dictionary<string, string?>());

        try
        {
            using var cts = new CancellationTokenSource(timeout);
            await process.Process.WaitForExitAsync(cts.Token);

            var (stdout, stderr) = await process.GetOutputAsync();
            await process.WriteLogsAsync(artifactsRoot);

            if (process.Process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Process '{name}' failed with exit code {process.Process.ExitCode}.{Environment.NewLine}" +
                    $"stdout:{Environment.NewLine}{stdout}{Environment.NewLine}" +
                    $"stderr:{Environment.NewLine}{stderr}");
            }
        }
        finally
        {
            await process.DisposeAsync();
        }
    }

    private static ManagedProcess StartProcess(
        string name,
        string fileName,
        string arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string?> environmentVariables)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        foreach (var pair in environmentVariables)
        {
            process.StartInfo.Environment[pair.Key] = pair.Value;
        }

        process.Start();
        var stdOutTask = process.StandardOutput.ReadToEndAsync();
        var stdErrTask = process.StandardError.ReadToEndAsync();

        return new ManagedProcess(name, process, stdOutTask, stdErrTask);
    }

    private static async Task<JsonDocument> SendJsonAsync(
        HttpClient httpClient,
        HttpMethod method,
        string url,
        object? body = null,
        string? bearerToken = null)
    {
        using var response = await SendRawAsync(httpClient, method, url, bearerToken, body);
        response.EnsureSuccessStatusCode();
        return await ParseJsonAsync(response);
    }

    private static async Task<HttpResponseMessage> SendRawAsync(
        HttpClient httpClient,
        HttpMethod method,
        string url,
        string? bearerToken = null,
        object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await httpClient.SendAsync(request);
    }

    private static async Task<JsonDocument> ParseJsonAsync(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }

    private static async Task<int> CountExternalIdentitiesAsync(string libsqlUrl)
    {
        using var client = new NelknetLibsqlDatabaseClient(libsqlUrl);
        var result = await client.ExecuteAsync("SELECT COUNT(*) AS Count FROM ExternalIdentities");
        return result.Rows[0].GetInt32("Count");
    }

    private static int GetFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    private sealed class ManagedProcess : IAsyncDisposable
    {
        private readonly Task<string> _stdOutTask;
        private readonly Task<string> _stdErrTask;

        public ManagedProcess(string name, Process process, Task<string> stdOutTask, Task<string> stdErrTask)
        {
            Name = name;
            Process = process;
            _stdOutTask = stdOutTask;
            _stdErrTask = stdErrTask;
        }

        public string Name { get; }
        public Process Process { get; }

        public async Task<(string StdOut, string StdErr)> GetOutputAsync()
        {
            return (await _stdOutTask, await _stdErrTask);
        }

        public async Task WriteLogsAsync(string directory)
        {
            var (stdout, stderr) = await GetOutputAsync();
            await File.WriteAllTextAsync(Path.Combine(directory, $"{Name}.stdout.log"), stdout, Encoding.UTF8);
            await File.WriteAllTextAsync(Path.Combine(directory, $"{Name}.stderr.log"), stderr, Encoding.UTF8);
        }

        public async ValueTask DisposeAsync()
        {
            if (!Process.HasExited)
            {
                Process.Kill(entireProcessTree: true);
                await Process.WaitForExitAsync();
            }

            Process.Dispose();
        }
    }

    private sealed class LocalJwksServer : IAsyncDisposable
    {
        private readonly HttpListener _listener;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _loopTask;
        private readonly string _document;

        private LocalJwksServer(HttpListener listener, string document)
        {
            _listener = listener;
            _document = document;
            _loopTask = Task.Run(ListenAsync);
        }

        public string BaseUrl { get; private init; } = string.Empty;
        public string GoogleJwksUrl => $"{BaseUrl}google";
        public string MicrosoftJwksUrl => $"{BaseUrl}microsoft";

        public static Task<LocalJwksServer> StartAsync(string document)
        {
            var port = GetFreeTcpPort();
            var prefix = $"http://127.0.0.1:{port}/";
            var listener = new HttpListener();
            listener.Prefixes.Add(prefix);
            listener.Start();

            return Task.FromResult(new LocalJwksServer(listener, document)
            {
                BaseUrl = prefix
            });
        }

        private async Task ListenAsync()
        {
            while (!_cts.IsCancellationRequested)
            {
                HttpListenerContext? context = null;
                try
                {
                    context = await _listener.GetContextAsync();
                    context.Response.StatusCode = 200;
                    context.Response.ContentType = "application/json";

                    await using var writer = new StreamWriter(context.Response.OutputStream, Encoding.UTF8, 1024, leaveOpen: false);
                    await writer.WriteAsync(_document);
                }
                catch (HttpListenerException) when (_cts.IsCancellationRequested)
                {
                    return;
                }
                catch (ObjectDisposedException) when (_cts.IsCancellationRequested)
                {
                    return;
                }
                finally
                {
                    context?.Response.Close();
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            _listener.Close();
            await _loopTask;
            _cts.Dispose();
        }
    }

    private sealed class SigningKeyFixture : IAsyncDisposable
    {
        private readonly RSA _rsa = RSA.Create(2048);

        public SigningKeyFixture()
        {
            SigningKey = new RsaSecurityKey(_rsa)
            {
                KeyId = "test-key"
            };
        }

        public RsaSecurityKey SigningKey { get; }

        public string CreateToken(string issuer, string audience, IEnumerable<Claim> claims)
        {
            var descriptor = new SecurityTokenDescriptor
            {
                Issuer = issuer,
                Audience = audience,
                Expires = DateTime.UtcNow.AddMinutes(10),
                Subject = new ClaimsIdentity(claims),
                SigningCredentials = new SigningCredentials(SigningKey, SecurityAlgorithms.RsaSha256)
            };

            return new JsonWebTokenHandler().CreateToken(descriptor);
        }

        public string CreateJwksDocument()
        {
            var parameters = _rsa.ExportParameters(false);

            return $$"""
            {
              "keys": [
                {
                  "kty": "RSA",
                  "use": "sig",
                  "alg": "RS256",
                  "kid": "{{SigningKey.KeyId}}",
                  "n": "{{Base64UrlEncoder.Encode(parameters.Modulus)}}",
                  "e": "{{Base64UrlEncoder.Encode(parameters.Exponent)}}"
                }
              ]
            }
            """;
        }

        public ValueTask DisposeAsync()
        {
            _rsa.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
