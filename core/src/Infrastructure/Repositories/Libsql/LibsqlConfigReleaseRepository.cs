using Nona.Domain;
using Nona.Domain.Entities;
using Nona.Domain.Enums;
using Nona.Domain.Interfaces;
using Nona.Libsql;
using System.Globalization;

namespace Nona.Infrastructure.Repositories.Libsql;

public sealed class LibsqlConfigReleaseRepository : IConfigReleaseRepository
{
    private readonly ILibsqlDatabaseClient _client;

    public LibsqlConfigReleaseRepository(ILibsqlDatabaseClient client)
    {
        _client = client;
    }

    public async Task<ConfigRelease?> GetMetadataAsync(
        string projectName,
        string environmentName,
        string version,
        CancellationToken ct = default)
    {
        var result = await _client.ExecuteAsync(
            """
            WITH TargetRelease AS (
                SELECT Project, Environment, Version, Major, Minor, Patch, CreatedAt, Actor
                FROM ConfigReleases
                WHERE Project = @ProjectName COLLATE NOCASE
                  AND Environment = @EnvironmentName COLLATE NOCASE
                  AND Version = @Version COLLATE NOCASE
                LIMIT 1
            )
            SELECT
                release.Project,
                release.Environment,
                release.Version,
                release.Major,
                release.Minor,
                release.Patch,
                release.CreatedAt,
                release.Actor,
                (
                    SELECT COUNT(1)
                    FROM ConfigReleaseEntries entries
                    WHERE entries.Project = release.Project COLLATE NOCASE
                      AND entries.Environment = release.Environment COLLATE NOCASE
                      AND entries.ReleaseVersion = release.Version COLLATE NOCASE
                ) AS EntryCount
            FROM TargetRelease release
            """,
            CreateVersionParameters(projectName, environmentName, version),
            ct);

        return result.Rows.Count == 0
            ? null
            : MapRelease(result.Rows[0], []);
    }

    public async Task<ConfigRelease?> GetLatestPatchMetadataAsync(
        string projectName,
        string environmentName,
        int major,
        int minor,
        CancellationToken ct = default)
    {
        var result = await _client.ExecuteAsync(
            """
            WITH TargetRelease AS (
                SELECT Project, Environment, Version, Major, Minor, Patch, CreatedAt, Actor
                FROM ConfigReleases
                WHERE Project = @ProjectName COLLATE NOCASE
                  AND Environment = @EnvironmentName COLLATE NOCASE
                  AND Major = @Major
                  AND Minor = @Minor
                ORDER BY Patch DESC
                LIMIT 1
            )
            SELECT
                release.Project,
                release.Environment,
                release.Version,
                release.Major,
                release.Minor,
                release.Patch,
                release.CreatedAt,
                release.Actor,
                (
                    SELECT COUNT(1)
                    FROM ConfigReleaseEntries entries
                    WHERE entries.Project = release.Project COLLATE NOCASE
                      AND entries.Environment = release.Environment COLLATE NOCASE
                      AND entries.ReleaseVersion = release.Version COLLATE NOCASE
                ) AS EntryCount
            FROM TargetRelease release
            """,
            LibsqlParameters.Create(
                ("ProjectName", projectName),
                ("EnvironmentName", environmentName),
                ("Major", major),
                ("Minor", minor)),
            ct);

        return result.Rows.Count == 0
            ? null
            : MapRelease(result.Rows[0], []);
    }

    public async Task<ConfigRelease?> GetAsync(
        string projectName,
        string environmentName,
        string version,
        CancellationToken ct = default)
    {
        var release = await GetMetadataAsync(projectName, environmentName, version, ct);
        if (release is null)
            return null;

        var entries = await ListEntriesAsync(projectName, environmentName, release.Version, ct);
        return WithEntries(release, entries);
    }

    public async Task<ConfigRelease?> GetLatestPatchAsync(
        string projectName,
        string environmentName,
        int major,
        int minor,
        CancellationToken ct = default)
    {
        var release = await GetLatestPatchMetadataAsync(projectName, environmentName, major, minor, ct);
        if (release is null)
            return null;

        var entries = await ListEntriesAsync(projectName, environmentName, release.Version, ct);
        return WithEntries(release, entries);
    }

    public Task<ConfigReleaseEntryLookupResult> GetEntryAsync(
        string projectName,
        string environmentName,
        string version,
        string key,
        KeyScope requiredScope,
        CancellationToken ct = default)
    {
        return ExecuteEntryLookupAsync(
            """
            SELECT
                releases.Project,
                releases.Environment,
                releases.Version AS ReleaseVersion,
                EXISTS (
                    SELECT 1
                    FROM ConfigReleaseEntries pending
                    WHERE pending.Project = releases.Project COLLATE NOCASE
                      AND pending.Environment = releases.Environment COLLATE NOCASE
                      AND pending.ReleaseVersion = releases.Version COLLATE NOCASE
                      AND pending.NormalizedKey IS NULL
                ) AS PendingKeyBackfill,
                entries.Key,
                entries.Value,
                entries.ContentType,
                entries.Scope
            FROM ConfigReleases releases
            LEFT JOIN ConfigReleaseEntries entries
              ON entries.Project = releases.Project COLLATE NOCASE
             AND entries.Environment = releases.Environment COLLATE NOCASE
             AND entries.ReleaseVersion = releases.Version COLLATE NOCASE
             AND entries.NormalizedKey = @NormalizedKey
             AND (entries.Scope & @RequiredScope) != 0
            WHERE releases.Project = @ProjectName COLLATE NOCASE
              AND releases.Environment = @EnvironmentName COLLATE NOCASE
              AND releases.Version = @Version COLLATE NOCASE
            ORDER BY entries.Key
            """,
            LibsqlParameters.Create(
                ("ProjectName", projectName),
                ("EnvironmentName", environmentName),
                ("Version", version),
                ("NormalizedKey", NormalizeKey(key)),
                ("RequiredScope", (int)requiredScope)),
            key,
            requiredScope,
            ct);
    }

    public Task<ConfigReleaseEntryLookupResult> GetLatestPatchEntryAsync(
        string projectName,
        string environmentName,
        int major,
        int minor,
        string key,
        KeyScope requiredScope,
        CancellationToken ct = default)
    {
        return ExecuteEntryLookupAsync(
            """
            SELECT
                releases.Project,
                releases.Environment,
                releases.Version AS ReleaseVersion,
                EXISTS (
                    SELECT 1
                    FROM ConfigReleaseEntries pending
                    WHERE pending.Project = releases.Project COLLATE NOCASE
                      AND pending.Environment = releases.Environment COLLATE NOCASE
                      AND pending.ReleaseVersion = releases.Version COLLATE NOCASE
                      AND pending.NormalizedKey IS NULL
                ) AS PendingKeyBackfill,
                entries.Key,
                entries.Value,
                entries.ContentType,
                entries.Scope
            FROM (
                SELECT Project, Environment, Version
                FROM ConfigReleases
                WHERE Project = @ProjectName COLLATE NOCASE
                  AND Environment = @EnvironmentName COLLATE NOCASE
                  AND Major = @Major
                  AND Minor = @Minor
                ORDER BY Patch DESC
                LIMIT 1
            ) releases
            LEFT JOIN ConfigReleaseEntries entries
              ON entries.Project = releases.Project COLLATE NOCASE
             AND entries.Environment = releases.Environment COLLATE NOCASE
             AND entries.ReleaseVersion = releases.Version COLLATE NOCASE
             AND entries.NormalizedKey = @NormalizedKey
             AND (entries.Scope & @RequiredScope) != 0
            ORDER BY entries.Key
            """,
            LibsqlParameters.Create(
                ("ProjectName", projectName),
                ("EnvironmentName", environmentName),
                ("Major", major),
                ("Minor", minor),
                ("NormalizedKey", NormalizeKey(key)),
                ("RequiredScope", (int)requiredScope)),
            key,
            requiredScope,
            ct);
    }

    public async Task<IReadOnlyList<ConfigRelease>> ListAsync(string projectName, string environmentName, CancellationToken ct = default)
    {
        var result = await _client.ExecuteAsync(
            """
            SELECT
                Project,
                Environment,
                Version,
                Major,
                Minor,
                Patch,
                CreatedAt,
                Actor,
                (
                    SELECT COUNT(1)
                    FROM ConfigReleaseEntries entries
                    WHERE entries.Project = ConfigReleases.Project COLLATE NOCASE
                      AND entries.Environment = ConfigReleases.Environment COLLATE NOCASE
                      AND entries.ReleaseVersion = ConfigReleases.Version COLLATE NOCASE
                ) AS EntryCount
            FROM ConfigReleases
            WHERE Project = @ProjectName COLLATE NOCASE
              AND Environment = @EnvironmentName COLLATE NOCASE
            ORDER BY Major DESC, Minor DESC, Patch DESC
            """,
            LibsqlParameters.Create(
                ("ProjectName", projectName),
                ("EnvironmentName", environmentName)),
            ct);

        return result.Rows.Select(row => MapRelease(row, [])).ToList();
    }

    public async Task<IReadOnlyList<ConfigReleaseEntry>> ListEntriesAsync(
        string projectName,
        string environmentName,
        string version,
        KeyScope requiredScope,
        CancellationToken ct = default)
    {
        var result = await _client.ExecuteAsync(
            """
            SELECT Project, Environment, ReleaseVersion, Key, Value, ContentType, Scope
            FROM ConfigReleaseEntries
            WHERE Project = @ProjectName COLLATE NOCASE
              AND Environment = @EnvironmentName COLLATE NOCASE
              AND ReleaseVersion = @Version COLLATE NOCASE
              AND (Scope & @RequiredScope) != 0
            ORDER BY Key
            """,
            LibsqlParameters.Create(
                ("ProjectName", projectName),
                ("EnvironmentName", environmentName),
                ("Version", version),
                ("RequiredScope", (int)requiredScope)),
            ct);

        return result.Rows.Select(MapEntry).ToList();
    }

    public async Task<bool> ExistsAsync(string projectName, string environmentName, string version, CancellationToken ct = default)
    {
        var result = await _client.ExecuteAsync(
            """
            SELECT COUNT(1)
            FROM ConfigReleases
            WHERE Project = @ProjectName COLLATE NOCASE
              AND Environment = @EnvironmentName COLLATE NOCASE
              AND Version = @Version COLLATE NOCASE
            """,
            CreateVersionParameters(projectName, environmentName, version),
            ct);

        return result.Rows[0].GetInt32(0) > 0;
    }

    public async Task<bool> AddAsync(ConfigRelease release, CancellationToken ct = default)
    {
        EnsureUniqueEntryKeys(release);

        var statements = new List<LibsqlStatement>
        {
            new(
                """
                INSERT OR IGNORE INTO ConfigReleases (Project, Environment, Version, Major, Minor, Patch, CreatedAt, Actor)
                VALUES (@Project, @Environment, @Version, @Major, @Minor, @Patch, @CreatedAt, @Actor)
                """,
                ToReleaseParameters(release))
        };

        foreach (var entry in release.Entries)
        {
            statements.Add(new LibsqlStatement(
                """
                INSERT OR IGNORE INTO ConfigReleaseEntries (
                    Project, Environment, ReleaseVersion, Key, NormalizedKey, Value, ContentType, Scope
                )
                VALUES (
                    @Project, @Environment, @ReleaseVersion, @Key, @NormalizedKey, @Value, @ContentType, @Scope
                )
                """,
                ToEntryParameters(entry)));
        }

        var results = await _client.ExecuteBatchAsync(statements, ct);
        return results[0].AffectedRowCount > 0;
    }

    public async Task<bool> DeleteAsync(string projectName, string environmentName, string version, CancellationToken ct = default)
    {
        var parameters = CreateVersionParameters(projectName, environmentName, version);
        var results = await _client.ExecuteBatchAsync(
        [
            new LibsqlStatement(
                """
                DELETE FROM ConfigReleaseEntries
                WHERE Project = @ProjectName COLLATE NOCASE
                  AND Environment = @EnvironmentName COLLATE NOCASE
                  AND ReleaseVersion = @Version COLLATE NOCASE
                """,
                parameters),
            new LibsqlStatement(
                """
                DELETE FROM ConfigReleases
                WHERE Project = @ProjectName COLLATE NOCASE
                  AND Environment = @EnvironmentName COLLATE NOCASE
                  AND Version = @Version COLLATE NOCASE
                """,
                parameters)
        ],
        ct);

        return results[^1].AffectedRowCount > 0;
    }

    public async Task DeleteByEnvironmentAsync(string projectName, string environmentName, CancellationToken ct = default)
    {
        await _client.ExecuteBatchAsync(
            [
                new LibsqlStatement(
                    """
                    DELETE FROM ConfigReleaseEntries
                    WHERE Project = @ProjectName COLLATE NOCASE
                      AND Environment = @EnvironmentName COLLATE NOCASE
                    """,
                    LibsqlParameters.Create(
                        ("ProjectName", projectName),
                        ("EnvironmentName", environmentName))),
                new LibsqlStatement(
                    """
                    DELETE FROM ConfigReleases
                    WHERE Project = @ProjectName COLLATE NOCASE
                      AND Environment = @EnvironmentName COLLATE NOCASE
                    """,
                    LibsqlParameters.Create(
                        ("ProjectName", projectName),
                        ("EnvironmentName", environmentName)))
            ],
            ct);
    }

    public async Task DeleteByProjectAsync(string projectName, CancellationToken ct = default)
    {
        await _client.ExecuteBatchAsync(
            [
                new LibsqlStatement(
                    """
                    DELETE FROM ConfigReleaseEntries
                    WHERE Project = @ProjectName COLLATE NOCASE
                    """,
                    LibsqlParameters.Create(("ProjectName", projectName))),
                new LibsqlStatement(
                    """
                    DELETE FROM ConfigReleases
                    WHERE Project = @ProjectName COLLATE NOCASE
                    """,
                    LibsqlParameters.Create(("ProjectName", projectName)))
            ],
            ct);
    }

    private async Task<IReadOnlyList<ConfigReleaseEntry>> ListEntriesAsync(
        string projectName,
        string environmentName,
        string version,
        CancellationToken ct)
    {
        var result = await _client.ExecuteAsync(
            """
            SELECT Project, Environment, ReleaseVersion, Key, Value, ContentType, Scope
            FROM ConfigReleaseEntries
            WHERE Project = @ProjectName COLLATE NOCASE
              AND Environment = @EnvironmentName COLLATE NOCASE
              AND ReleaseVersion = @Version COLLATE NOCASE
            ORDER BY Key
            """,
            CreateVersionParameters(projectName, environmentName, version),
            ct);

        return result.Rows.Select(MapEntry).ToList();
    }

    private async Task<ConfigReleaseEntryLookupResult> ExecuteEntryLookupAsync(
        string sql,
        IReadOnlyDictionary<string, object?> parameters,
        string key,
        KeyScope requiredScope,
        CancellationToken ct)
    {
        var result = await _client.ExecuteAsync(sql, parameters, ct);
        if (result.Rows.Count == 0)
        {
            return new ConfigReleaseEntryLookupResult(false, null);
        }

        if (result.Rows[0].GetInt32("PendingKeyBackfill") != 0)
        {
            throw new InvalidOperationException("Release entry keys require database initialization.");
        }

        var row = result.Rows.FirstOrDefault(candidate =>
            candidate.GetValue("Key") is not null
            && candidate.GetString("Key").Equals(key, StringComparison.OrdinalIgnoreCase));
        var entry = row is null ? null : MapEntry(row);
        if (entry is not null && (entry.Scope & requiredScope) == 0)
        {
            entry = null;
        }

        return new ConfigReleaseEntryLookupResult(true, entry);
    }

    private static ConfigRelease MapRelease(LibsqlRow row, IReadOnlyList<ConfigReleaseEntry> entries)
    {
        return new ConfigRelease
        {
            Project = row.GetString("Project"),
            Environment = row.GetString("Environment"),
            Version = row.GetString("Version"),
            Major = row.GetInt32("Major"),
            Minor = row.GetInt32("Minor"),
            Patch = row.GetInt32("Patch"),
            Entries = entries,
            EntryCount = row.GetInt32("EntryCount"),
            CreatedAt = ParseTimestamp(row.GetString("CreatedAt")),
            Actor = row.GetString("Actor")
        };
    }

    private static ConfigRelease WithEntries(
        ConfigRelease release,
        IReadOnlyList<ConfigReleaseEntry> entries)
    {
        return new ConfigRelease
        {
            Project = release.Project,
            Environment = release.Environment,
            Version = release.Version,
            Major = release.Major,
            Minor = release.Minor,
            Patch = release.Patch,
            Entries = entries,
            EntryCount = release.EntryCount,
            CreatedAt = release.CreatedAt,
            Actor = release.Actor
        };
    }

    private static ConfigReleaseEntry MapEntry(LibsqlRow row)
    {
        return new ConfigReleaseEntry
        {
            Project = row.GetString("Project"),
            Environment = row.GetString("Environment"),
            ReleaseVersion = row.GetString("ReleaseVersion"),
            Key = row.GetString("Key"),
            Value = row.GetString("Value"),
            ContentType = row.GetString("ContentType"),
            Scope = (KeyScope)row.GetInt32("Scope")
        };
    }

    private static IReadOnlyDictionary<string, object?> CreateVersionParameters(
        string projectName,
        string environmentName,
        string version)
    {
        return LibsqlParameters.Create(
            ("ProjectName", projectName),
            ("EnvironmentName", environmentName),
            ("Version", version));
    }

    private static IReadOnlyDictionary<string, object?> ToReleaseParameters(ConfigRelease release)
    {
        return LibsqlParameters.Create(
            ("Project", release.Project),
            ("Environment", release.Environment),
            ("Version", release.Version),
            ("Major", release.Major),
            ("Minor", release.Minor),
            ("Patch", release.Patch),
            ("CreatedAt", release.CreatedAt.ToString("O")),
            ("Actor", release.Actor));
    }

    private static IReadOnlyDictionary<string, object?> ToEntryParameters(ConfigReleaseEntry entry)
    {
        return LibsqlParameters.Create(
            ("Project", entry.Project),
            ("Environment", entry.Environment),
            ("ReleaseVersion", entry.ReleaseVersion),
            ("Key", entry.Key),
            ("NormalizedKey", NormalizeKey(entry.Key)),
            ("Value", entry.Value),
            ("ContentType", entry.ContentType),
            ("Scope", (int)entry.Scope));
    }

    private static DateTime ParseTimestamp(string value)
        => DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    internal static string NormalizeKey(string key) => key.ToUpperInvariant();

    private static void EnsureUniqueEntryKeys(ConfigRelease release)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in release.Entries)
        {
            ConfigEntryKey.ThrowIfInvalid(entry.Key, nameof(release));
            if (!keys.Add(entry.Key))
            {
                throw new ArgumentException("Release entries must have unique case-insensitive keys.", nameof(release));
            }
        }
    }
}
