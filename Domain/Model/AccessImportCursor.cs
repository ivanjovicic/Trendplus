using System.ComponentModel.DataAnnotations;

namespace Domain.Model;

public sealed class AccessImportCursor
{
    [Key]
    [MaxLength(128)]
    public string TableKey { get; set; } = string.Empty;

    // timestamp | id | none | timestamp_then_id | id_or_composite
    [MaxLength(32)]
    public string CursorMode { get; set; } = "id";
    [MaxLength(256)]
    public string? SourceKey { get; set; }
    [MaxLength(128)]
    public string? CursorTimestampColumn { get; set; }
    [MaxLength(128)]
    public string? CursorIdColumn { get; set; }

    public DateTime? CursorTimestampUtc { get; set; }
    public long? CursorId { get; set; }
    public long? CursorTieBreakerId { get; set; }
    public int OverlapSeconds { get; set; } = 60;
    public int LastRowsRead { get; set; }
    public int LastRowsMerged { get; set; }
    public int? LastLagSeconds { get; set; }
    public long? LastSuccessfulBatchId { get; set; }
    public DateTime? LastRunStartedAtUtc { get; set; }
    public DateTime? LastRunCompletedAtUtc { get; set; }
    [MaxLength(200)]
    public string? LeaseOwner { get; set; }
    public DateTime? LeaseAcquiredAtUtc { get; set; }
    public DateTime? LeaseExpiresAtUtc { get; set; }

    [MaxLength(2000)]
    public string? LastError { get; set; }

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
