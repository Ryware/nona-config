namespace Nona.Domain.Entities;

public sealed record ConfigReleaseEntryLookupResult(
    bool ReleaseFound,
    ConfigReleaseEntry? Entry);
