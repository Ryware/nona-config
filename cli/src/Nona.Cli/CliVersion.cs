using System.Reflection;

namespace Nona.Cli;

internal static class CliVersion
{
    private const string VersionEnvironmentVariable = "NONA_CLI_VERSION";

    internal static string GetDisplayVersion()
    {
        var overriddenVersion = Environment.GetEnvironmentVariable(VersionEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(overriddenVersion))
            return overriddenVersion.Trim();

        var informationalVersion = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
            return informationalVersion.Split('+', 2)[0];

        return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
    }

    internal static bool IsVersionRequest(string[] args)
        => args.Length == 1 &&
           (string.Equals(args[0], "--version", StringComparison.Ordinal) ||
            string.Equals(args[0], "-v", StringComparison.Ordinal));
}
