using System.ComponentModel.DataAnnotations;

namespace Domain.Model;

/// <summary>
/// Durable source checkpoint. Identity is ConnectionId + MappingProfileId + SourceStream.
/// Dedicated deployments use TenantScope = n/a_dedicated. Shared SaaS requires MT07.
/// A checkpoint is the last source position whose destination effects were committed.
/// </summary>
public sealed class SourceSyncCheckpoint
{
    [MaxLength(128)]
    public string ConnectionId { get; set; } = string.Empty;

    [MaxLength(64)]
    public string MappingProfileId { get; set; } = string.Empty;

    [MaxLength(128)]
    public string SourceStream { get; set; } = string.Empty;

    [MaxLength(32)]
    public string CursorMode { get; set; } = "id";

    public DateTime? CursorTimestampUtc { get; set; }

    [MaxLength(256)]
    public string? ExternalKeyTieBreaker { get; set; }

    public int OverlapSeconds { get; set; } = 60;

    [MaxLength(80)]
    public string? SchemaFingerprint { get; set; }

    public Guid? LastStartedBatchId { get; set; }

    public Guid? LastCompletedBatchId { get; set; }

    public DateTime? LastSuccessfulSyncUtc { get; set; }

    [MaxLength(64)]
    public string? FailureCategory { get; set; }

    [MaxLength(2000)]
    public string? LastError { get; set; }

    [MaxLength(32)]
    public string TenantScope { get; set; } = "n/a_dedicated";

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
