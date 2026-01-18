using Microsoft.Extensions.Hosting;
using Nona.Infrastructure.Repositories.Sqlite;

namespace Nona.Infrastructure.Services;

public class SqliteDatabaseInitializer : IHostedService
{
    private readonly SqliteDbContext _dbContext;
    private readonly string _migrationsFolder;

    public SqliteDatabaseInitializer(SqliteDbContext dbContext)
    {
        _dbContext = dbContext;
        
        // Get migrations folder path relative to the application
        var basePath = AppDomain.CurrentDomain.BaseDirectory;
        _migrationsFolder = Path.Combine(basePath, "Migrations");
        
        // If not found, try relative to source (for development)
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
            Console.WriteLine($"??  Migrations folder not found at: {_migrationsFolder}");
            Console.WriteLine("??  Skipping migrations...");
            return;
        }

        Console.WriteLine($"?? Migrations folder: {_migrationsFolder}");
        await _dbContext.InitializeDatabaseAsync(_migrationsFolder, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
