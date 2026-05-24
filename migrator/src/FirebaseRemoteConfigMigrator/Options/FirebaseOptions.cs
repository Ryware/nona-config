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
