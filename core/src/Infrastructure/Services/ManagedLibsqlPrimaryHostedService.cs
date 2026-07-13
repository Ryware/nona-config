using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nona.Libsql;

namespace Nona.Infrastructure.Services;

public sealed class ManagedLibsqlPrimaryHostedService : IHostedService, IDisposable
{
    private readonly LibsqlManagedPrimaryOptions _options;
    private readonly ILogger<ManagedLibsqlPrimaryHostedService> _logger;

    private Process? _process;
    private Task? _stdoutPump;
    private Task? _stderrPump;

    public ManagedLibsqlPrimaryHostedService(
        IOptions<LibsqlOptions> options,
        ILogger<ManagedLibsqlPrimaryHostedService> logger)
    {
        _options = options.Value.ManagedPrimary;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return;
        }

        var databasePath = _options.ResolveDatabasePath();
        var workingDirectory = _options.ResolveWorkingDirectory();
        var connectUrl = _options.ResolveLocalConnectUrl();

        Directory.CreateDirectory(Path.GetDirectoryName(databasePath) ?? workingDirectory);
        Directory.CreateDirectory(workingDirectory);

        var startInfo = new ProcessStartInfo
        {
            FileName = _options.ExecutablePath,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var argument in ManagedLibsqlPrimaryProcessArguments.Build(_options))
        {
            startInfo.ArgumentList.Add(argument);
        }

        var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        process.Exited += (_, _) =>
        {
            _logger.LogWarning(
                "Managed libSQL primary exited. ExitCode={ExitCode}",
                process.HasExited ? process.ExitCode : null);
        };

        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("Managed libSQL primary process did not start.");
            }
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException(
                $"Managed libSQL primary executable '{_options.ExecutablePath}' could not be started. " +
                "Install sqld or configure Storage:Libsql:ManagedPrimary:ExecutablePath.",
                ex);
        }

        _process = process;
        _stdoutPump = PumpOutputAsync(process.StandardOutput, isError: false);
        _stderrPump = PumpOutputAsync(process.StandardError, isError: true);

        _logger.LogInformation(
            "Starting managed libSQL primary with executable '{ExecutablePath}' on {ListenAddress}.",
            _options.ExecutablePath,
            _options.HttpListenAddress);

        await WaitUntilReadyAsync(connectUrl, cancellationToken);

        _logger.LogInformation(
            "Managed libSQL primary ready. Local connect URL: {ConnectUrl}",
            connectUrl);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_process is null)
        {
            return;
        }

        if (!_process.HasExited)
        {
            _logger.LogInformation("Stopping managed libSQL primary.");

            try
            {
                _process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }
        }

        try
        {
            await _process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }

        if (_stdoutPump is not null)
        {
            await Task.WhenAny(_stdoutPump, Task.Delay(TimeSpan.FromSeconds(1), CancellationToken.None));
        }

        if (_stderrPump is not null)
        {
            await Task.WhenAny(_stderrPump, Task.Delay(TimeSpan.FromSeconds(1), CancellationToken.None));
        }
    }

    public void Dispose()
    {
        _process?.Dispose();
    }

    private async Task WaitUntilReadyAsync(string connectUrl, CancellationToken cancellationToken)
    {
        using var startupCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        startupCts.CancelAfter(TimeSpan.FromSeconds(_options.StartTimeoutSeconds));

        Exception? lastError = null;

        while (!startupCts.IsCancellationRequested)
        {
            if (_process?.HasExited == true)
            {
                throw new InvalidOperationException(
                    $"Managed libSQL primary exited before becoming ready. ExitCode={_process.ExitCode}.",
                    lastError);
            }

            try
            {
                using var client = new NelknetLibsqlDatabaseClient(Options.Create(new LibsqlOptions
                {
                    DataSource = connectUrl,
                    TimeoutSeconds = 5
                }));
                var result = await client.ExecuteAsync("SELECT 1 AS Value", ct: startupCts.Token);
                if (result.Rows.Count == 1 && result.Rows[0].GetInt32("Value") == 1)
                {
                    return;
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                lastError = ex;
            }

            try
            {
                await Task.Delay(250, startupCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }

        throw new TimeoutException(
            $"Managed libSQL primary did not become ready within {_options.StartTimeoutSeconds} seconds.",
            lastError);
    }

    private async Task PumpOutputAsync(StreamReader reader, bool isError)
    {
        while (await reader.ReadLineAsync() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (isError)
            {
                _logger.LogWarning("sqld: {Line}", line);
            }
            else
            {
                _logger.LogInformation("sqld: {Line}", line);
            }
        }
    }
}

internal static class ManagedLibsqlPrimaryProcessArguments
{
    public static IReadOnlyList<string> Build(LibsqlManagedPrimaryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var arguments = new List<string>
        {
            "--db-path", options.ResolveDatabasePath(),
            "--http-listen-addr", options.HttpListenAddress
        };
        var extraArgs = options.ExtraArgs
            .Where(argument => !string.IsNullOrWhiteSpace(argument))
            .ToArray();

        AddDefaultOption(arguments, extraArgs, "--max-concurrent-connections", "512");
        AddDefaultOption(arguments, extraArgs, "--max-concurrent-requests", "512");
        AddDefaultOption(arguments, extraArgs, "--disable-intelligent-throttling");
        AddDefaultOption(arguments, extraArgs, "--connection-creation-timeout-sec", "4");

        if (extraArgs.Length > 0)
        {
            arguments.AddRange(extraArgs);
        }

        return arguments;
    }

    private static void AddDefaultOption(
        List<string> arguments,
        IReadOnlyCollection<string> extraArgs,
        string option,
        string? value = null)
    {
        if (extraArgs.Contains(option, StringComparer.Ordinal))
        {
            return;
        }

        arguments.Add(option);
        if (value is not null)
        {
            arguments.Add(value);
        }
    }
}
