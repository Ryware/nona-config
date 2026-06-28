using Microsoft.Extensions.Options;
using Nona.Libsql;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace Nona.Infrastructure.Tests.Common;

internal sealed class LocalSqldTestServer : IAsyncDisposable
{
    private readonly string _root;
    private readonly Process _process;
    private readonly Task<string> _stdoutTask;
    private readonly Task<string> _stderrTask;

    private LocalSqldTestServer(string root, Process process, int port)
    {
        _root = root;
        _process = process;
        _stdoutTask = process.StandardOutput.ReadToEndAsync();
        _stderrTask = process.StandardError.ReadToEndAsync();
        Url = $"http://127.0.0.1:{port}";
    }

    public string Url { get; }

    public static async Task<LocalSqldTestServer> StartAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), $"nona-sqld-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        var port = GetFreeTcpPort();
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "sqld",
                WorkingDirectory = root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.StartInfo.ArgumentList.Add("--db-path");
        process.StartInfo.ArgumentList.Add(Path.Combine(root, "primary.db"));
        process.StartInfo.ArgumentList.Add("--http-listen-addr");
        process.StartInfo.ArgumentList.Add($"127.0.0.1:{port}");
        process.StartInfo.ArgumentList.Add("--max-concurrent-connections");
        process.StartInfo.ArgumentList.Add("128");
        process.StartInfo.ArgumentList.Add("--max-concurrent-requests");
        process.StartInfo.ArgumentList.Add("128");
        process.StartInfo.ArgumentList.Add("--disable-intelligent-throttling");
        process.StartInfo.ArgumentList.Add("--connection-creation-timeout-sec");
        process.StartInfo.ArgumentList.Add("4");

        if (!process.Start())
        {
            throw new InvalidOperationException("sqld test process did not start.");
        }

        var server = new LocalSqldTestServer(root, process, port);
        await server.WaitUntilReadyAsync();
        return server;
    }

    public NelknetLibsqlDatabaseClient CreateClient()
    {
        return new NelknetLibsqlDatabaseClient(Options.Create(new LibsqlOptions
        {
            DataSource = Url,
            TimeoutSeconds = 30
        }));
    }

    public async ValueTask DisposeAsync()
    {
        if (!_process.HasExited)
        {
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync();
        }

        _process.Dispose();

        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch
        {
        }
    }

    private async Task WaitUntilReadyAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        Exception? lastError = null;

        while (!timeout.IsCancellationRequested)
        {
            if (_process.HasExited)
            {
                var stdout = await _stdoutTask;
                var stderr = await _stderrTask;
                throw new InvalidOperationException(
                    $"sqld exited before becoming ready. ExitCode={_process.ExitCode}{Environment.NewLine}" +
                    $"stdout:{Environment.NewLine}{stdout}{Environment.NewLine}" +
                    $"stderr:{Environment.NewLine}{stderr}");
            }

            try
            {
                using var client = CreateClient();
                var result = await client.ExecuteAsync("SELECT 1 AS Value", ct: timeout.Token);
                if (result.Rows.Count == 1 && result.Rows[0].GetInt32("Value") == 1)
                {
                    return;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastError = ex;
            }

            try
            {
                await Task.Delay(250, timeout.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        throw new TimeoutException("sqld test process did not become ready.", lastError);
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
}
