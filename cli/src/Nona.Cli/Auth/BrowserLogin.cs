using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;

namespace Nona.Cli;

internal static class BrowserLogin
{
    public static async Task<int> RunAsync(string baseUrl, CliSessionStore sessionStore, CancellationToken cancellationToken)
    {
        var state = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        var port = FindFreePort();
        var redirectUri = $"http://localhost:{port}/callback";
        var loginUrl = $"{baseUrl.TrimEnd('/')}/cli-login?cli_state={Uri.EscapeDataString(state)}&cli_redirect={Uri.EscapeDataString(redirectUri)}";

        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://localhost:{port}/");
        listener.Start();

        Console.WriteLine("Opening browser for login...");
        Process.Start(new ProcessStartInfo(loginUrl) { UseShellExecute = true });
        Console.WriteLine("Waiting for login... (Ctrl+C to cancel, times out in 5 minutes)");

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromMinutes(5));

        try
        {
            while (true)
            {
                var context = await WaitForContextAsync(listener, timeoutCts.Token);
                var path = context.Request.Url?.AbsolutePath ?? string.Empty;

                if (path != "/callback")
                {
                    context.Response.StatusCode = 404;
                    context.Response.Close();
                    continue;
                }

                var query = ParseQueryString(context.Request.Url?.Query?.TrimStart('?') ?? string.Empty);
                query.TryGetValue("token", out var token);
                query.TryGetValue("state", out var returnedState);
                query.TryGetValue("username", out var username);
                query.TryGetValue("role", out var role);
                query.TryGetValue("expires_at", out var expiresAtStr);

                if (!string.Equals(returnedState, state, StringComparison.Ordinal) || string.IsNullOrWhiteSpace(token))
                {
                    await SendHtmlAsync(context.Response, CallbackErrorHtml);
                    Console.Error.WriteLine("Login failed: invalid callback.");
                    return 1;
                }

                await SendHtmlAsync(context.Response, CallbackSuccessHtml);

                if (!DateTime.TryParse(expiresAtStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var expiresAt))
                    expiresAt = DateTime.UtcNow.AddHours(24);

                sessionStore.Save(new CliAuthSession
                {
                    BaseUrl = baseUrl,
                    Token = token,
                    Username = username ?? string.Empty,
                    Role = role ?? string.Empty,
                    ExpiresAt = expiresAt,
                    SavedAtUtc = DateTime.UtcNow
                });

                Console.WriteLine($"Logged in as {username}");
                Console.WriteLine($"Role: {role}");
                Console.WriteLine($"Expires at: {expiresAt:O}");
                Console.WriteLine($"Session file: {sessionStore.FilePath}");
                return 0;
            }
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine(cancellationToken.IsCancellationRequested ? "Cancelled." : "Login timed out.");
            return 1;
        }
        finally
        {
            listener.Stop();
        }
    }

    private static async Task<HttpListenerContext> WaitForContextAsync(HttpListener listener, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<bool>();
        await using var _ = cancellationToken.Register(() => tcs.TrySetResult(false));
        var contextTask = listener.GetContextAsync();
        await Task.WhenAny(contextTask, tcs.Task);
        cancellationToken.ThrowIfCancellationRequested();
        return await contextTask;
    }

    private static int FindFreePort()
    {
        using var tcp = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        tcp.Start();
        var port = ((System.Net.IPEndPoint)tcp.LocalEndpoint).Port;
        tcp.Stop();
        return port;
    }

    private static IReadOnlyDictionary<string, string> ParseQueryString(string query) =>
        query.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=', 2))
            .Where(p => p.Length == 2)
            .ToDictionary(
                p => Uri.UnescapeDataString(p[0]),
                p => Uri.UnescapeDataString(p[1]),
                StringComparer.OrdinalIgnoreCase);

    private static async Task SendHtmlAsync(HttpListenerResponse response, string html)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(html);
        response.ContentType = "text/html; charset=utf-8";
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes);
        response.Close();
    }

    private const string CallbackSuccessHtml = @"<!DOCTYPE html>
<html><head><meta charset='UTF-8'><title>Nona CLI</title>
<style>body{font-family:-apple-system,sans-serif;display:flex;align-items:center;justify-content:center;min-height:100vh;margin:0;background:#f5f5f5}.card{background:#fff;border-radius:12px;padding:40px;text-align:center;max-width:360px;box-shadow:0 4px 16px rgba(0,0,0,.1)}h1{font-size:20px;margin-bottom:8px}p{color:#666;font-size:14px}</style>
</head><body><div class='card'><h1>Logged in.</h1><p>You can close this tab and return to your terminal.</p></div></body></html>";

    private const string CallbackErrorHtml = @"<!DOCTYPE html>
<html><head><meta charset='UTF-8'><title>Nona CLI</title>
<style>body{font-family:-apple-system,sans-serif;display:flex;align-items:center;justify-content:center;min-height:100vh;margin:0;background:#f5f5f5}.card{background:#fff;border-radius:12px;padding:40px;text-align:center;max-width:360px;box-shadow:0 4px 16px rgba(0,0,0,.1)}h1{font-size:20px;margin-bottom:8px;color:#c00}p{color:#666;font-size:14px}</style>
</head><body><div class='card'><h1>Login failed.</h1><p>Please close this tab and try again.</p></div></body></html>";
}
