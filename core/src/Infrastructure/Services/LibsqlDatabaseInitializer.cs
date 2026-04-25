using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Nona.Libsql;

namespace Nona.Infrastructure.Services;

public sealed class LibsqlDatabaseInitializer : IHostedService
{
    private readonly ILibsqlDatabaseClient _client;
    private readonly string _migrationsFolder;
    private readonly bool _skipMigrations;

    public LibsqlDatabaseInitializer(
        ILibsqlDatabaseClient client,
        IOptions<LibsqlOptions> options)
    {
        _client = client;
        _skipMigrations = options.Value.EnableLocalReplica;

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
        if (_skipMigrations)
        {
            return;
        }

        if (!Directory.Exists(_migrationsFolder))
        {
            Console.WriteLine($"Migrations folder not found at: {_migrationsFolder}");
            Console.WriteLine("Skipping libSQL migrations.");
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
