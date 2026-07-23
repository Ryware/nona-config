using Nona.Cli.Generated.Models;

namespace Nona.Cli.Releases;

internal static class ReleaseRenderer
{
    public static void WriteSummary(ConfigReleaseDto release)
    {
        var active = release.IsActive == true ? " (active)" : string.Empty;
        Console.WriteLine($"  {release.Version}{active}");
        Console.WriteLine($"    Entries: {CliUntypedNode.FormatInteger(release.EntryCount)}");
        Console.WriteLine($"    Created: {release.CreatedAt:u}");
        Console.WriteLine($"    Actor:   {release.Actor}");
    }

    public static void WriteDetails(ConfigReleaseDetailsDto release)
    {
        Console.WriteLine(
            $"Release {release.Version} — {release.Project} / {release.Environment}");
        Console.WriteLine($"  Active:  {(release.IsActive == true ? "yes" : "no")}");
        Console.WriteLine($"  Entries: {CliUntypedNode.FormatInteger(release.EntryCount)}");
        Console.WriteLine($"  Created: {release.CreatedAt:u}");
        Console.WriteLine($"  Actor:   {release.Actor}");

        if (release.Entries is null || release.Entries.Count == 0)
        {
            Console.WriteLine("  No entries.");
            return;
        }

        Console.WriteLine("  Entries:");
        foreach (var entry in release.Entries.OrderBy(entry => entry.Key))
        {
            Console.WriteLine($"    {entry.Key} = {entry.Value}");
            Console.WriteLine($"      Type: {entry.ContentType}; Scope: {entry.Scope}");
        }
    }
}
