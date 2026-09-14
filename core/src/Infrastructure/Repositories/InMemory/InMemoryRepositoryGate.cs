namespace Nona.Infrastructure.Repositories.InMemory;

// Capability issuance and resource deletion must not interleave their dictionary writes.
internal static class InMemoryRepositoryGate
{
    internal static object SyncRoot { get; } = new();
}
