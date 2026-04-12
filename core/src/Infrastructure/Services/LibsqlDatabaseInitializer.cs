using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Nona.Libsql;

namespace Nona.Infrastructure.Services;

public sealed class LibsqlDatabaseInitializer : IHostedService
{
    private readonly LibsqlHttpDatabaseClient _client;
    private readonly IServiceProvider _serviceProvider;
    private readonly LibsqlOptions _options;
    private readonly string _migrationsFolder;

    public LibsqlDatabaseInitializer(
        LibsqlHttpDatabaseClient client,
        IServiceProvider serviceProvider,
        IOptions<LibsqlOptions> options)
    {
        _client = client;
        _serviceProvider = serviceProvider;
        _options = options.Value;

        var basePath = AppDomain.CurrentDomain.BaseDirectory;
        _migrationsFolder = Path.Combine(basePath, "Migrations");

        if (!Directory.Exists(_migrationsFolder))
        {
            var currentDir = Directory.GetCurrentDirectory();
            _migrationsFolder = Path.GetFullPath(Path.Combine(currentDir, "..", "Infrastructure", "Migrations"));
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_migrationsFolder))
        {
            Console.WriteLine($"Migrations folder not found at: {_migrationsFolder}");
            Console.WriteLine("Skipping libSQL migrations.");
            return;
        }

        var mirroredClient = _serviceProvider.GetService<LibsqlMirroredLocalDatabaseClient>();
        if (_options.EnableLocalReplica)
        {
            if (mirroredClient is null)
            {
                throw new InvalidOperationException("Local replica mode is enabled, but the mirrored libSQL client was not registered.");
            }

            await mirroredClient.InitializeAsync(cancellationToken);
            return;
        }

        var directRunner = new LibsqlMigrationRunner(_client, _migrationsFolder);
        await directRunner.RunMigrationsAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
