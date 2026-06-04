using Nona.FirebaseRemoteConfigMigrator.Models;

namespace Nona.FirebaseRemoteConfigMigrator.Options;

public sealed record FirebaseOptions
{
    public string ProjectId { get; init; } = string.Empty;
    public string? Namespace { get; init; }
    public IReadOnlyList<FirebaseImportSource> Sources { get; init; } = [];
    public string? ServiceAccountJsonPath { get; init; }
    public string? ServiceAccountJson { get; init; }

    public IReadOnlyList<FirebaseImportSource> GetImportSources()
    {
        if (Sources.Count > 0)
            return Sources;

        if (!string.IsNullOrWhiteSpace(Namespace))
        {
            return
            [
                new FirebaseImportSource
                {
                    Namespace = Namespace,
                    Scope = "all"
                }
            ];
        }

        return
        [
            new FirebaseImportSource
            {
                Namespace = "firebase",
                Scope = "client"
            },
            new FirebaseImportSource
            {
                Namespace = "firebase-server",
                Scope = "server"
            }
        ];
    }
}
