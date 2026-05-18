namespace Nona.Libsql.Tests.Common;

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

    public static string ResolveMigrationsFolder()
    {
        var outputFolder = Path.Combine(AppContext.BaseDirectory, "Migrations");
        if (Directory.Exists(outputFolder))
        {
            return outputFolder;
        }

        return Path.Combine(
            ResolveRepoRoot(),
            "core",
            "src",
            "Infrastructure",
            "Migrations");
    }
}
