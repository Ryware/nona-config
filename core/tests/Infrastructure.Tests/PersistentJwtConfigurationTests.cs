using Microsoft.Extensions.Configuration;
using Nona.Infrastructure.Configuration;

namespace Nona.Infrastructure.Tests;

[NotInParallel]
public class PersistentJwtConfigurationTests
{
    private static readonly string[] JwtEnvironmentVariables =
    [
        "Jwt__Key",
        "Jwt__Issuer",
        "Jwt__Audience"
    ];

    [Test]
    public async Task Apply_GeneratesInMemoryJwtSettings_WhenStorageIsInMemory()
    {
        var originalEnvironment = ClearJwtEnvironment();
        var generatedDirectory = Path.Combine(Path.GetTempPath(), $"nona-jwt-inmemory-{Guid.NewGuid():N}");

        try
        {
            var configuration = new ConfigurationManager();
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:Type"] = "InMemory",
                ["Storage:Libsql:ManagedPrimary:Enabled"] = "true",
                ["Storage:Libsql:ManagedPrimary:DatabasePath"] = Path.Combine(generatedDirectory, "primary.db")
            });

            PersistentJwtConfiguration.Apply(configuration);

            await Assert.That(configuration["Jwt:Key"]).IsNotNull();
            await Assert.That(configuration["Jwt:Issuer"]).IsEqualTo("nona");
            await Assert.That(configuration["Jwt:Audience"]).IsEqualTo("nona");
            await Assert.That(Directory.Exists(generatedDirectory)).IsFalse();
        }
        finally
        {
            RestoreJwtEnvironment(originalEnvironment);
            DeleteDirectoryIfExists(generatedDirectory);
        }
    }

    [Test]
    public async Task Apply_PersistsGeneratedJwtSettings_WhenManagedLibsqlStorageIsConfigured()
    {
        var originalEnvironment = ClearJwtEnvironment();
        var generatedDirectory = Path.Combine(Path.GetTempPath(), $"nona-jwt-libsql-{Guid.NewGuid():N}");

        try
        {
            var configuration = new ConfigurationManager();
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:Type"] = "Libsql",
                ["Storage:Libsql:ManagedPrimary:Enabled"] = "true",
                ["Storage:Libsql:ManagedPrimary:DatabasePath"] = Path.Combine(generatedDirectory, "primary.db")
            });

            PersistentJwtConfiguration.Apply(configuration);

            await Assert.That(configuration["Jwt:Key"]).IsNotNull();
            await Assert.That(configuration["Jwt:Issuer"]).IsEqualTo("nona");
            await Assert.That(configuration["Jwt:Audience"]).IsEqualTo("nona");
            await Assert.That(File.Exists(Path.Combine(generatedDirectory, "jwt.generated.json"))).IsTrue();
        }
        finally
        {
            RestoreJwtEnvironment(originalEnvironment);
            DeleteDirectoryIfExists(generatedDirectory);
        }
    }

    [Test]
    public async Task Apply_PersistsGeneratedJwtSettingsBesideAutoSelectedSqliteDatabase()
    {
        var originalEnvironment = ClearJwtEnvironment();
        var generatedDirectory = Path.Combine(Path.GetTempPath(), $"nona-jwt-sqlite-{Guid.NewGuid():N}");

        try
        {
            var configuration = new ConfigurationManager();
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:Type"] = "Auto",
                ["Storage:Sqlite:DataSource"] = Path.Combine(generatedDirectory, "nona.db")
            });

            PersistentJwtConfiguration.Apply(configuration);

            await Assert.That(configuration["Jwt:Key"]).IsNotNull();
            await Assert.That(File.Exists(Path.Combine(generatedDirectory, "jwt.generated.json"))).IsTrue();
        }
        finally
        {
            RestoreJwtEnvironment(originalEnvironment);
            DeleteDirectoryIfExists(generatedDirectory);
        }
    }

    [Test]
    public async Task Apply_UsesCompleteJwtEnvironmentOverride_WithoutRequiringGeneratedConfigPath()
    {
        var originalEnvironment = ClearJwtEnvironment();

        try
        {
            Environment.SetEnvironmentVariable("Jwt__Key", "environment-signing-key");
            Environment.SetEnvironmentVariable("Jwt__Issuer", "environment-issuer");
            Environment.SetEnvironmentVariable("Jwt__Audience", "environment-audience");

            var configuration = new ConfigurationManager();

            PersistentJwtConfiguration.Apply(configuration);

            await Assert.That(configuration["Storage:Libsql:ManagedPrimary:DatabasePath"]).IsNull();
        }
        finally
        {
            RestoreJwtEnvironment(originalEnvironment);
        }
    }

    private static string?[] ClearJwtEnvironment()
    {
        var originalValues = new string?[JwtEnvironmentVariables.Length];

        for (var index = 0; index < JwtEnvironmentVariables.Length; index++)
        {
            originalValues[index] = Environment.GetEnvironmentVariable(JwtEnvironmentVariables[index]);
            Environment.SetEnvironmentVariable(JwtEnvironmentVariables[index], null);
        }

        return originalValues;
    }

    private static void RestoreJwtEnvironment(IReadOnlyList<string?> originalValues)
    {
        for (var index = 0; index < JwtEnvironmentVariables.Length; index++)
        {
            Environment.SetEnvironmentVariable(JwtEnvironmentVariables[index], originalValues[index]);
        }
    }

    private static void DeleteDirectoryIfExists(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
