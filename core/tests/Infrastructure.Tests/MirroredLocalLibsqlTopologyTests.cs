using Microsoft.Data.Sqlite;
using Nona.Infrastructure.Tests.Common;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Nona.Infrastructure.Tests;

public class MirroredLocalLibsqlTopologyTests
{
    [Test]
    public async Task MirroredLocalReplicaMode_KeepsTwoBackendsInSync_AndServesCachedReadsWhenPrimaryIsDown()
    {
        if (!OperatingSystem.IsWindows())
        {
            Console.WriteLine("Skipping mirrored-local libSQL topology test because the runtime is not Windows.");
            return;
        }

        var artifactsRoot = Path.Combine(Path.GetTempPath(), $"nona-mirrored-local-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(artifactsRoot);

        var success = false;
        ManagedProcess? backendA = null;
        ManagedProcess? backendB = null;

        try
        {
            await RunProcessCheckedAsync(
                "build",
                "dotnet",
                $"build \"{TestPaths.ResolveWebApiProject()}\"",
                TestPaths.ResolveRepoRoot(),
                artifactsRoot,
                TimeSpan.FromMinutes(5));

            var backendAPort = GetFreeTcpPort();
            var backendBPort = GetFreeTcpPort();

            var replicaAPath = Path.Combine(artifactsRoot, "replica-a.db");
            var replicaBPath = Path.Combine(artifactsRoot, "replica-b.db");

            backendA = await StartPrimaryBackendAsync("backend-a", backendAPort, replicaAPath, artifactsRoot);
            backendB = await StartReplicaBackendAsync(
                "backend-b",
                backendBPort,
                backendAPort,
                replicaBPath,
                artifactsRoot);

            using var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };

            var baseA = $"http://127.0.0.1:{backendAPort}";
            var baseB = $"http://127.0.0.1:{backendBPort}";

            var suffix = Guid.NewGuid().ToString("N")[..12];
            var email = $"mirrored-local-{suffix}@example.com";
            var password = "MirrorCheck123!";
            var project = $"mirror-{suffix}";
            var offlineProject = $"mirror-offline-{suffix}";
            const string environmentName = "Production";
            var key = $"KEY_{suffix}";
            var value = $"value-from-a-{suffix}";
            var offlineKey = $"OFFLINE_{suffix}";
            var offlineValue = $"offline-value-{suffix}";

            using var register = await SendJsonAsync(
                httpClient,
                HttpMethod.Post,
                $"{baseA}/auth/register",
                new { email, password });
            await Assert.That(register.RootElement.GetProperty("success").GetBoolean()).IsTrue();

            var tokenA = register.RootElement
                .GetProperty("response")
                .GetProperty("token")
                .GetString() ?? throw new InvalidOperationException("Register response did not include a token.");

            using var login = await SendJsonAsync(
                httpClient,
                HttpMethod.Post,
                $"{baseB}/auth/login",
                new { email, password });
            var tokenB = login.RootElement.GetProperty("token").GetString()
                ?? throw new InvalidOperationException("Login response did not include a token.");

            using var createProject = await SendJsonAsync(
                httpClient,
                HttpMethod.Post,
                $"{baseA}/admin/projects",
                new { name = project },
                tokenA);
            await Assert.That(createProject.RootElement.GetProperty("name").GetString()).IsEqualTo(project);

            using var projectsOnB = await SendJsonAsync(
                httpClient,
                HttpMethod.Get,
                $"{baseB}/admin/projects",
                bearerToken: tokenB);
            await Assert.That(ContainsNamedElement(projectsOnB.RootElement, project)).IsTrue();

            using var environmentsOnB = await SendJsonAsync(
                httpClient,
                HttpMethod.Get,
                $"{baseB}/admin/projects/{project}/environments",
                bearerToken: tokenB);
            await Assert.That(ContainsNamedElement(environmentsOnB.RootElement, environmentName)).IsTrue();

            using var createConfig = await SendJsonAsync(
                httpClient,
                HttpMethod.Put,
                $"{baseB}/admin/projects/{project}/environments/{environmentName}/config-entries/{key}",
                new
                {
                    value,
                    contentType = "string",
                    scope = "all"
                },
                tokenB);
            await Assert.That(createConfig.RootElement.GetProperty("value").GetString()).IsEqualTo(value);

            using var configOnA = await SendJsonAsync(
                httpClient,
                HttpMethod.Get,
                $"{baseA}/admin/projects/{project}/environments/{environmentName}/config-entries/{key}",
                bearerToken: tokenA);
            await Assert.That(configOnA.RootElement.GetProperty("value").GetString()).IsEqualTo(value);

            await WaitForReplicaCountAsync(
                replicaBPath,
                "SELECT COUNT(1) FROM Projects WHERE UrlSlug = @Slug COLLATE NOCASE",
                1,
                new Dictionary<string, object?> { ["Slug"] = project });
            await WaitForReplicaCountAsync(
                replicaBPath,
                "SELECT COUNT(1) FROM ConfigEntries WHERE Project = @Project COLLATE NOCASE AND Environment = @Environment COLLATE NOCASE AND Key = @Key COLLATE NOCASE",
                1,
                new Dictionary<string, object?>
                {
                    ["Project"] = project,
                    ["Environment"] = environmentName,
                    ["Key"] = key
                });
            await WaitForReplicaCountAsync(
                replicaAPath,
                "SELECT COUNT(1) FROM ConfigEntries WHERE Project = @Project COLLATE NOCASE AND Environment = @Environment COLLATE NOCASE AND Key = @Key COLLATE NOCASE",
                1,
                new Dictionary<string, object?>
                {
                    ["Project"] = project,
                    ["Environment"] = environmentName,
                    ["Key"] = key
                });

            await SendWithoutBodyAsync(
                httpClient,
                HttpMethod.Delete,
                $"{baseA}/admin/projects/{project}/environments/{environmentName}/config-entries/{key}",
                tokenA,
                HttpStatusCode.NoContent);

            using (var configDeletedOnB = await SendRawAsync(
                httpClient,
                HttpMethod.Get,
                $"{baseB}/admin/projects/{project}/environments/{environmentName}/config-entries/{key}",
                tokenB))
            {
                await Assert.That(configDeletedOnB.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
            }

            await WaitForReplicaCountAsync(
                replicaBPath,
                "SELECT COUNT(1) FROM ConfigEntries WHERE Project = @Project COLLATE NOCASE AND Environment = @Environment COLLATE NOCASE AND Key = @Key COLLATE NOCASE",
                0,
                new Dictionary<string, object?>
                {
                    ["Project"] = project,
                    ["Environment"] = environmentName,
                    ["Key"] = key
                });
            await WaitForReplicaCountAsync(
                replicaAPath,
                "SELECT COUNT(1) FROM ConfigEntries WHERE Project = @Project COLLATE NOCASE AND Environment = @Environment COLLATE NOCASE AND Key = @Key COLLATE NOCASE",
                0,
                new Dictionary<string, object?>
                {
                    ["Project"] = project,
                    ["Environment"] = environmentName,
                    ["Key"] = key
                });

            await SendWithoutBodyAsync(
                httpClient,
                HttpMethod.Delete,
                $"{baseB}/admin/projects/{project}",
                tokenB,
                HttpStatusCode.NoContent);

            using var projectsOnAAfterDelete = await SendJsonAsync(
                httpClient,
                HttpMethod.Get,
                $"{baseA}/admin/projects",
                bearerToken: tokenA);
            await Assert.That(ContainsNamedElement(projectsOnAAfterDelete.RootElement, project)).IsFalse();

            await WaitForReplicaCountAsync(
                replicaAPath,
                "SELECT COUNT(1) FROM Projects WHERE UrlSlug = @Slug COLLATE NOCASE",
                0,
                new Dictionary<string, object?> { ["Slug"] = project });
            await WaitForReplicaCountAsync(
                replicaBPath,
                "SELECT COUNT(1) FROM Projects WHERE UrlSlug = @Slug COLLATE NOCASE",
                0,
                new Dictionary<string, object?> { ["Slug"] = project });

            using var createOfflineProject = await SendJsonAsync(
                httpClient,
                HttpMethod.Post,
                $"{baseA}/admin/projects",
                new { name = offlineProject },
                tokenA);

            using var createOfflineConfig = await SendJsonAsync(
                httpClient,
                HttpMethod.Put,
                $"{baseA}/admin/projects/{offlineProject}/environments/{environmentName}/config-entries/{offlineKey}",
                new
                {
                    value = offlineValue,
                    contentType = "string",
                    scope = "all"
                },
                tokenA);

            using var projectsWithOfflineOnB = await SendJsonAsync(
                httpClient,
                HttpMethod.Get,
                $"{baseB}/admin/projects",
                bearerToken: tokenB);
            await Assert.That(ContainsNamedElement(projectsWithOfflineOnB.RootElement, offlineProject)).IsTrue();

            using var offlineConfigOnB = await SendJsonAsync(
                httpClient,
                HttpMethod.Get,
                $"{baseB}/admin/projects/{offlineProject}/environments/{environmentName}/config-entries/{offlineKey}",
                bearerToken: tokenB);
            await Assert.That(offlineConfigOnB.RootElement.GetProperty("value").GetString()).IsEqualTo(offlineValue);

            await WaitForReplicaCountAsync(
                replicaBPath,
                "SELECT COUNT(1) FROM Projects WHERE UrlSlug = @Slug COLLATE NOCASE",
                1,
                new Dictionary<string, object?> { ["Slug"] = offlineProject });
            await WaitForReplicaCountAsync(
                replicaBPath,
                "SELECT COUNT(1) FROM ConfigEntries WHERE Project = @Project COLLATE NOCASE AND Environment = @Environment COLLATE NOCASE AND Key = @Key COLLATE NOCASE",
                1,
                new Dictionary<string, object?>
                {
                    ["Project"] = offlineProject,
                    ["Environment"] = environmentName,
                    ["Key"] = offlineKey
                });

            await backendA.DisposeAsync();
            backendA = null;

            await backendB.DisposeAsync();
            backendB = null;
            backendB = await StartReplicaBackendAsync(
                "backend-b-restarted",
                backendBPort,
                backendAPort,
                replicaBPath,
                artifactsRoot);

            using var loginWhilePrimaryDown = await SendJsonAsync(
                httpClient,
                HttpMethod.Post,
                $"{baseB}/auth/login",
                new { email, password });
            var tokenWhilePrimaryDown = loginWhilePrimaryDown.RootElement.GetProperty("token").GetString()
                ?? throw new InvalidOperationException("Login response did not include a token after replica restart.");

            using var projectsWhilePrimaryDown = await SendJsonAsync(
                httpClient,
                HttpMethod.Get,
                $"{baseB}/admin/projects",
                bearerToken: tokenWhilePrimaryDown);
            await Assert.That(ContainsNamedElement(projectsWhilePrimaryDown.RootElement, offlineProject)).IsTrue();

            using var configWhilePrimaryDown = await SendJsonAsync(
                httpClient,
                HttpMethod.Get,
                $"{baseB}/admin/projects/{offlineProject}/environments/{environmentName}/config-entries/{offlineKey}",
                bearerToken: tokenWhilePrimaryDown);
            await Assert.That(configWhilePrimaryDown.RootElement.GetProperty("value").GetString()).IsEqualTo(offlineValue);

            using var failedWriteWhilePrimaryDown = await SendRawAsync(
                httpClient,
                HttpMethod.Delete,
                $"{baseB}/admin/projects/{offlineProject}/environments/{environmentName}/config-entries/{offlineKey}",
                tokenWhilePrimaryDown);
            await Assert.That(failedWriteWhilePrimaryDown.StatusCode).IsEqualTo(HttpStatusCode.InternalServerError);

            await WaitForReplicaCountAsync(
                replicaBPath,
                "SELECT COUNT(1) FROM ConfigEntries WHERE Project = @Project COLLATE NOCASE AND Environment = @Environment COLLATE NOCASE AND Key = @Key COLLATE NOCASE",
                1,
                new Dictionary<string, object?>
                {
                    ["Project"] = offlineProject,
                    ["Environment"] = environmentName,
                    ["Key"] = offlineKey
                });

            await Assert.That(File.Exists(replicaAPath)).IsTrue();
            await Assert.That(File.Exists(replicaBPath)).IsTrue();
            await Assert.That(new FileInfo(replicaAPath).Length).IsGreaterThan(0L);
            await Assert.That(new FileInfo(replicaBPath).Length).IsGreaterThan(0L);

            success = true;
        }
        finally
        {
            if (backendB is not null)
            {
                await backendB.DisposeAsync();
            }

            if (backendA is not null)
            {
                await backendA.DisposeAsync();
            }

            if (!success)
            {
                Console.WriteLine($"Mirrored-local topology artifacts kept at: {artifactsRoot}");
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

    private static async Task<ManagedProcess> StartPrimaryBackendAsync(
        string name,
        int port,
        string replicaPath,
        string artifactsRoot)
    {
        var process = StartProcess(
            name,
            "dotnet",
            $"\"{TestPaths.ResolveWebApiOutputAssembly()}\"",
            TestPaths.ResolveWebApiWorkingDirectory(),
            new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Development",
                ["ASPNETCORE_URLS"] = $"http://127.0.0.1:{port}",
                ["Storage__Type"] = "Libsql",
                ["Storage__Libsql__AuthToken"] = "dev-token",
                ["Storage__Libsql__EnableLocalReplica"] = "true",
                ["Jwt__Key"] = "mirrored-local-tests-signing-key-1234567890",
                ["Jwt__Issuer"] = "mirrored-local-tests",
                ["Jwt__Audience"] = "mirrored-local-tests",
                ["Storage__Libsql__LocalReplicaPath"] = replicaPath,
                ["Storage__Libsql__LocalReplicaRole"] = "Primary"
            });

        return await StartBackendAndWaitAsync(process, name, port, artifactsRoot);
    }

    private static async Task<ManagedProcess> StartReplicaBackendAsync(
        string name,
        int port,
        int primaryPort,
        string replicaPath,
        string artifactsRoot)
    {
        var process = StartProcess(
            name,
            "dotnet",
            $"\"{TestPaths.ResolveWebApiOutputAssembly()}\"",
            TestPaths.ResolveWebApiWorkingDirectory(),
            new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Development",
                ["ASPNETCORE_URLS"] = $"http://127.0.0.1:{port}",
                ["Storage__Type"] = "Libsql",
                ["ConnectionStrings__Libsql"] = $"http://127.0.0.1:{primaryPort}/internal/libsql",
                ["Storage__Libsql__AuthToken"] = "dev-token",
                ["Storage__Libsql__EnableLocalReplica"] = "true",
                ["Jwt__Key"] = "mirrored-local-tests-signing-key-1234567890",
                ["Jwt__Issuer"] = "mirrored-local-tests",
                ["Jwt__Audience"] = "mirrored-local-tests",
                ["Storage__Libsql__LocalReplicaPath"] = replicaPath,
                ["Storage__Libsql__LocalReplicaRole"] = "Replica"
            });

        return await StartBackendAndWaitAsync(process, name, port, artifactsRoot);
    }

    private static async Task<ManagedProcess> StartBackendAndWaitAsync(
        ManagedProcess process,
        string name,
        int port,
        string artifactsRoot)
    {
        try
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
                        $"Backend '{name}' exited before becoming healthy.{Environment.NewLine}" +
                        $"stdout:{Environment.NewLine}{stdout}{Environment.NewLine}" +
                        $"stderr:{Environment.NewLine}{stderr}");
                }

                try
                {
                    using var response = await httpClient.GetAsync(healthUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        return process;
                    }
                }
                catch
                {
                }

                await Task.Delay(250);
            }

            await process.WriteLogsAsync(artifactsRoot);
            throw new TimeoutException($"Timed out waiting for backend '{name}' at {healthUrl}.");
        }
        catch
        {
            await process.WriteLogsAsync(artifactsRoot);
            await process.DisposeAsync();
            throw;
        }
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
        await using var stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
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

    private static async Task SendWithoutBodyAsync(
        HttpClient httpClient,
        HttpMethod method,
        string url,
        string bearerToken,
        HttpStatusCode expectedStatus)
    {
        using var response = await SendRawAsync(httpClient, method, url, bearerToken);
        await Assert.That(response.StatusCode).IsEqualTo(expectedStatus);
    }

    private static bool ContainsNamedElement(JsonElement root, string name)
    {
        if (root.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var element in root.EnumerateArray())
        {
            if (element.ValueKind == JsonValueKind.Object &&
                element.TryGetProperty("name", out var nameProperty) &&
                string.Equals(nameProperty.GetString(), name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static async Task WaitForReplicaCountAsync(
        string replicaPath,
        string sql,
        int expectedCount,
        IReadOnlyDictionary<string, object?> parameters,
        TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                if (File.Exists(replicaPath))
                {
                    var count = await QueryReplicaScalarAsync(replicaPath, sql, parameters);
                    if (count == expectedCount)
                    {
                        return;
                    }
                }
            }
            catch
            {
            }

            await Task.Delay(200);
        }

        throw new InvalidOperationException(
            $"Replica '{replicaPath}' did not reach expected count {expectedCount} for query:{Environment.NewLine}{sql}");
    }

    private static async Task<int> QueryReplicaScalarAsync(
        string replicaPath,
        string sql,
        IReadOnlyDictionary<string, object?> parameters)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = replicaPath,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString();

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue($"@{parameter.Key}", parameter.Value ?? DBNull.Value);
        }

        var value = await command.ExecuteScalarAsync();
        return Convert.ToInt32(value);
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
}
