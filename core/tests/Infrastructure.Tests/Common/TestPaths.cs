namespace Nona.Infrastructure.Tests.Common;

internal static class TestPaths
{
    public static string ResolveRepoRoot()
    {
        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            ".."));
    }

    public static string ResolveWebApiProject()
    {
        return Path.Combine(
            ResolveRepoRoot(),
            "core",
            "src",
            "WebApi",
            "WebApi.csproj");
    }

    public static string ResolveWebApiOutputAssembly()
    {
        return Path.Combine(
            ResolveRepoRoot(),
            "core",
            "src",
            "WebApi",
            "bin",
            "Debug",
            "net10.0",
            "Nona.WebApi.dll");
    }

    public static string ResolveWebApiWorkingDirectory()
    {
        return Path.Combine(
            ResolveRepoRoot(),
            "core",
            "src",
            "WebApi");
    }
}
