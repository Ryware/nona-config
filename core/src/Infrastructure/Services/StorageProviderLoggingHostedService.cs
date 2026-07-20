using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nona.Infrastructure.Configuration;

namespace Nona.Infrastructure.Services;

public sealed class StorageProviderLoggingHostedService(
    StorageProviderResolution resolution,
    ILogger<StorageProviderLoggingHostedService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("{StorageProviderResolution}", resolution.Message);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
