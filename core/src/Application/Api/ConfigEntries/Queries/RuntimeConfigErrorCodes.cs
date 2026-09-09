namespace Nona.Application.Api.ConfigEntries.Queries;

public static class RuntimeConfigErrorCodes
{
    public const string ActiveReleaseNotConfigured = "active_release_not_configured";
    public const string ReleaseNotFound = "release_not_found";
    public const string ConfigEntryNotFound = "config_entry_not_found";
    public const string EnvironmentNotFound = "environment_not_found";
    public const string InvalidReleaseVersion = "invalid_release_version";
    public const string InvalidPrefix = "invalid_prefix";
    public const string InvalidApiKey = "invalid_api_key";
}
