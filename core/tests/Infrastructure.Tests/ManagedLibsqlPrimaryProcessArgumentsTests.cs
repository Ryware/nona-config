using Nona.Libsql;
using System.Reflection;

namespace Nona.Infrastructure.Tests;

public sealed class ManagedLibsqlPrimaryProcessArgumentsTests
{
    [Test]
    public async Task Build_DoesNotDuplicateDefaultOptions_WhenExtraArgsOverrideThem()
    {
        var arguments = Build(new LibsqlManagedPrimaryOptions
        {
            DatabasePath = "primary.db",
            HttpListenAddress = "127.0.0.1:9080",
            ExtraArgs =
            [
                "--grpc-listen-addr",
                "0.0.0.0:5001",
                "--max-concurrent-connections",
                "4096",
                "--max-concurrent-requests",
                "4096",
                "--connection-creation-timeout-sec",
                "10"
            ]
        });

        await Assert.That(arguments.Count(argument => argument == "--max-concurrent-connections")).IsEqualTo(1);
        await Assert.That(arguments.Count(argument => argument == "--max-concurrent-requests")).IsEqualTo(1);
        await Assert.That(arguments.Count(argument => argument == "--connection-creation-timeout-sec")).IsEqualTo(1);
        await Assert.That(arguments).Contains("4096");
        await Assert.That(arguments).Contains("10");
    }

    [Test]
    public async Task Build_AddsDefaultOptions_WhenExtraArgsDoNotOverrideThem()
    {
        var arguments = Build(new LibsqlManagedPrimaryOptions
        {
            DatabasePath = "primary.db",
            HttpListenAddress = "127.0.0.1:9080",
            ExtraArgs =
            [
                "--grpc-listen-addr",
                "0.0.0.0:5001"
            ]
        });

        await Assert.That(arguments).Contains("--max-concurrent-connections");
        await Assert.That(arguments).Contains("--max-concurrent-requests");
        await Assert.That(arguments).Contains("--disable-intelligent-throttling");
        await Assert.That(arguments).Contains("--connection-creation-timeout-sec");
        await Assert.That(arguments).Contains("512");
        await Assert.That(arguments).Contains("4");
    }

    private static IReadOnlyList<string> Build(LibsqlManagedPrimaryOptions options)
    {
        var type = typeof(Nona.Infrastructure.ConfigureServices).Assembly.GetType(
            "Nona.Infrastructure.Services.ManagedLibsqlPrimaryProcessArguments",
            throwOnError: true)!;
        var method = type.GetMethod("Build", BindingFlags.Public | BindingFlags.Static)!;
        return (IReadOnlyList<string>)method.Invoke(null, [options])!;
    }
}
