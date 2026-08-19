using System.ComponentModel.DataAnnotations;

namespace Domain.Model;

/// <summary>
/// Idempotent destination row for a source stream. Retry/overlap must update or skip, never duplicate.
/// Identity is ConnectionId + MappingProfileId + SourceStream + ExternalKey.
/// </summary>
public sealed class SourceSyncAppliedRow
{
    [MaxLength(128)]
    public string ConnectionId { get; set; } = string.Empty;

    [MaxLength(64)]
    public string MappingProfileId { get; set; } = string.Empty;

    [MaxLength(128)]
    public string SourceStream { get; set; } = string.Empty;

    [MaxLength(256)]
    public string ExternalKey { get; set; } = string.Empty;

    [MaxLength(80)]
    public string PayloadHash { get; set; } = string.Empty;

    public DateTime? CursorTimestampUtc { get; set; }

    public Guid LastBatchId { get; set; }

    [MaxLength(16)]
    public string ApplyStatus { get; set; } = "inserted";

    [MaxLength(64)]
    public string? RejectionReason { get; set; }

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
