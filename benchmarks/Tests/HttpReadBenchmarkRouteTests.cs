using System.Reflection;
using Nona.Benchmarks;

namespace Nona.Benchmarks.Tests;

public sealed class HttpReadBenchmarkRouteTests
{
    [Test]
    public async Task FullEnvironmentReadUsesWorkingParametersRoute()
    {
        var path = InvokeRouteBuilder(
            "BuildFullEnvironmentRequestPath",
            DatabaseSeeder.DatasetRows[DatasetSize.Small]);

        await Assert.That(path).IsEqualTo("/api/keys-1/parameters");
    }

    [Test]
    public async Task SingleKeyReadUsesWorkingParameterRoute()
    {
        var path = InvokeRouteBuilder(
            "BuildSingleKeyRequestPath",
            DatabaseSeeder.DatasetRows[DatasetSize.Large]);

        await Assert.That(path).IsEqualTo("/api/keys-10000/parameters/KEY_0000001");
    }

    private static string InvokeRouteBuilder(string methodName, int datasetKeyCount)
    {
        var method = typeof(HttpReadBenchmarkApp).GetMethod(
            methodName,
            BindingFlags.NonPublic | BindingFlags.Static);
        if (method is null)
            throw new MissingMethodException(typeof(HttpReadBenchmarkApp).FullName, methodName);

        return (string)method.Invoke(null, [datasetKeyCount])!;
    }
}
