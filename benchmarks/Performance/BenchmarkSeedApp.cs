namespace Nona.Benchmarks;

public static class BenchmarkSeedApp
{
    public static async Task<int> RunAsync(string[] args)
    {
        try
        {
            var options = ParseOptions(args);
            var migrationsDirectory = Path.Combine(
                ResolveRepoRoot(),
                "core",
                "src",
                "Infrastructure",
                "Migrations");

            using var cancellationSource = new CancellationTokenSource();
            Console.CancelKeyPress += (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                cancellationSource.Cancel();
            };

            if (options.SqlitePath is not null)
            {
                var sqlitePath = Path.GetFullPath(options.SqlitePath);
                Console.WriteLine($"Creating benchmark SQLite database at {sqlitePath}.");
                await DatabaseSeeder.CreateSeedDatabaseAsync(
                    sqlitePath,
                    migrationsDirectory,
                    cancellationSource.Token);
            }
            else
            {
                Console.WriteLine($"Seeding benchmark data through {options.LibsqlUrl}.");
                using var client = SqlStatementFactory.CreateDirectClient(
                    options.LibsqlUrl!,
                    options.LibsqlAuthToken);
                await DatabaseSeeder.SeedLibsqlDatabaseAsync(
                    client,
                    migrationsDirectory,
                    cancellationSource.Token);
            }

            Console.WriteLine(
                $"Seeded {string.Join(", ", DatabaseSeeder.DatasetRows.Values.Select(count => $"{count:N0} keys"))}.");
            return 0;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Benchmark seeding cancelled.");
            return 1;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static SeedOptions ParseOptions(string[] args)
    {
        string? sqlitePath = null;
        string? libsqlUrl = null;
        var libsqlAuthToken = string.Empty;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--sqlite-path":
                    sqlitePath = ReadValue(args, ref index);
                    break;
                case "--libsql-url":
                    libsqlUrl = ReadValue(args, ref index);
                    break;
                case "--libsql-auth-token":
                    libsqlAuthToken = ReadValue(args, ref index);
                    break;
                default:
                    throw new ArgumentException($"Unknown argument '{args[index]}'.");
            }
        }

        if (string.IsNullOrWhiteSpace(sqlitePath) == string.IsNullOrWhiteSpace(libsqlUrl))
        {
            throw new ArgumentException(
                "Specify exactly one seed target: --sqlite-path or --libsql-url.");
        }

        return new SeedOptions(sqlitePath, libsqlUrl, libsqlAuthToken);
    }

    private static string ReadValue(string[] args, ref int index)
    {
        if (++index >= args.Length)
        {
            throw new ArgumentException($"Missing value for '{args[index - 1]}'.");
        }

        return args[index];
    }

    private static string ResolveRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "NonaConfig.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not find repository root containing NonaConfig.slnx.");
    }

    private sealed record SeedOptions(
        string? SqlitePath,
        string? LibsqlUrl,
        string LibsqlAuthToken);
}

