using System.ComponentModel.DataAnnotations;

namespace Domain.Model.Analytics;

public sealed class AnalyticsCostSnapshotBatch
{
    [Key]
    public long Id { get; set; }

    [MaxLength(50)]
    public string Scope { get; set; } = "access_origin";

    [MaxLength(20)]
    public string Status { get; set; } = "draft";

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? GeneratedAtUtc { get; set; }
    public DateTime? ActivatedAtUtc { get; set; }
    public DateTime? DeactivatedAtUtc { get; set; }

    [MaxLength(100)]
    public string CreatedBy { get; set; } = "system";

    public string? Description { get; set; }

    public int RowCount { get; set; }
    public decimal TotalRevenueCovered { get; set; }
    public double CoveragePct { get; set; }
    public double NoCostPct { get; set; }
    public int? GenerationDurationMs { get; set; }
    public bool DryRun { get; set; }
    public string? ErrorMessage { get; set; }
    public string? MetadataJson { get; set; }

    // Navigation
    public ICollection<AnalyticsSaleLineCostSnapshot> Snapshots { get; set; } = new List<AnalyticsSaleLineCostSnapshot>();
}
