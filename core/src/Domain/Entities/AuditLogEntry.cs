using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nona.Domain.Entities;

public class AuditLogEntry
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    public required string Actor { get; init; }

    public bool ActorIsSystem { get; init; }

    public required string Action { get; init; }

    public required string Target { get; init; }

    public string? Project { get; init; }

    public string? Environment { get; init; }

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
