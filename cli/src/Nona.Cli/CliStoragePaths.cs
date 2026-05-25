namespace Nona.Cli;

internal static class CliStoragePaths
{
    public static string ResolveBaseDirectory()
    {
        var applicationData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (!string.IsNullOrWhiteSpace(applicationData))
            return Path.Combine(applicationData, "nona");

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile))
            return Path.Combine(userProfile, ".nona");

        return Path.Combine(Directory.GetCurrentDirectory(), ".nona");
    }
}
