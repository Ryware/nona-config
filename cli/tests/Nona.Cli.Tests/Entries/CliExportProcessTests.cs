using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

#pragma warning disable TUnit0055

namespace Nona.Cli.Tests.Entries;

[NotInParallel]
public sealed class CliExportProcessTests
{
    [Test]
    public async Task Export_RedirectsUnicodeAsUtf8_EvenWithLegacyConsoleEncoding()
    {
        const string entries = """[{"key":"Ångström","value":"Привет 😀","contentType":"text","scope":"all"}]""";
        var previousEncoding = Console.OutputEncoding;
        var previousOut = Console.Out;
        var previousError = Console.Error;
        (int Code, byte[] Output, string Error) result;
        try
        {
            if (OperatingSystem.IsWindows())
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                Console.OutputEncoding = Encoding.GetEncoding(437);
            }
            result = await CliExportProcess.RunAsync(entries);
        }
        finally
        {
            Console.OutputEncoding = previousEncoding;
            Console.SetOut(previousOut);
            Console.SetError(previousError);
        }
        await Assert.That(result.Code).IsEqualTo(0);
        await Assert.That(result.Error).IsEmpty();
        await Assert.That(result.Output.SequenceEqual(Encoding.UTF8.GetBytes("Ångström=Привет 😀\n"))).IsTrue();
    }
}

internal static class CliExportProcess
{
    internal static async Task<(int Code, byte[] Output, string Error)> RunAsync(string entries, string? outputFile = null)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var serve = ServeAsync(listener, entries, timeout.Token);
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true
        };
        foreach (var arg in new[] { typeof(Nona.Cli.Program).Assembly.Location,
                     "entries", "export", "--base-url", $"http://127.0.0.1:{port}",
                     "--token", "test-token", "--project", "my-project", "--environment", "production" })
            start.ArgumentList.Add(arg);
        if (outputFile is not null)
        {
            start.ArgumentList.Add("--output-file");
            start.ArgumentList.Add(outputFile);
        }
        var isolatedPath = Path.Combine(Path.GetTempPath(), $"nona-export-{Guid.NewGuid():N}");
        start.Environment["NONA_CLI_CONFIG_PATH"] = isolatedPath + ".config";
        start.Environment["NONA_CLI_SESSION_PATH"] = isolatedPath + ".session";
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start the CLI.");
        process.StandardInput.Close();
        using var output = new MemoryStream();
        var readOutput = process.StandardOutput.BaseStream.CopyToAsync(output, timeout.Token);
        var readError = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            await readOutput;
            var error = await readError;
            // If the CLI fails before requesting entries, do not wait for an unused listener.
            if (process.ExitCode != 0) timeout.Cancel();
            else await serve;
            return (process.ExitCode, output.ToArray(), error);
        }
        finally
        {
            timeout.Cancel();
            listener.Stop();
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            try { await serve; }
            catch (OperationCanceledException) { }
            catch (SocketException) when (timeout.IsCancellationRequested) { }
        }
    }

    private static async Task ServeAsync(TcpListener listener, string entries, CancellationToken ct)
    {
        using var client = await listener.AcceptTcpClientAsync(ct);
        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
        var requestLine = await reader.ReadLineAsync(ct);
        if (requestLine != "GET /admin/projects/my-project/environments/production/config-entries HTTP/1.1")
            throw new InvalidOperationException($"Unexpected request: {requestLine}");
        while (!string.IsNullOrEmpty(await reader.ReadLineAsync(ct))) { }
        var body = Encoding.UTF8.GetBytes(entries);
        var headers = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 200 OK\r\nContent-Type: application/json; charset=utf-8\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(headers, ct);
        await stream.WriteAsync(body, ct);
    }
}
