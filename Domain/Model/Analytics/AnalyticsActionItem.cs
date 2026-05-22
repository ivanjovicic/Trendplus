namespace Domain.Model.Analytics;

/// <summary>
/// Centralized queue for analytics actions across all domains: dashboard, product, supplier, inventory, nivelacija, data quality.
/// This enables unified action tracking, status management, and orchestration.
/// </summary>
public class AnalyticsActionItem
{
    public long Id { get; set; }

    /// <summary>
    /// Source domain: "dashboard" | "product" | "supplier" | "inventory" | "nivelacija" | "data_quality"
    /// </summary>
    public required string SourceType { get; set; }

    /// <summary>
    /// Unique key within source domain (e.g., for inventory: "dopuna:{artikalId}:{storeId}")
    /// Must be idempotenent for duplicate detection.
    /// </summary>
    public required string SourceKey { get; set; }

    /// <summary>
    /// Optional numeric source identifier (e.g., product ID, supplier ID, SKU ID)
    /// </summary>
    public int? SourceId { get; set; }

    /// <summary>
    /// Action title (e.g., "Dopuni za Artikal X", "Smanji cenu Produkta Y")
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// Detailed recommendation description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Recommendation status label (e.g., "Neophodno odmah", "Preporučeno", "Opciono")
    /// </summary>
    public string? RecommendationStatus { get; set; }

    /// <summary>
    /// Priority level: "P1" (urgent) | "P2" (important) | "P3" (optional)
    /// </summary>
    public required string Priority { get; set; }

    /// <summary>
    /// Optional estimated financial impact in RSD (positive = gain, negative = cost)
    /// </summary>
    public decimal? ImpactEstimateRsd { get; set; }

    /// <summary>
    /// Confidence percentage (0-100) in the recommendation
    /// </summary>
    public int? ConfidencePct { get; set; }

    /// <summary>
    /// Data reliability percentage (0-100)
    /// </summary>
    public int? ReliabilityPct { get; set; }

    /// <summary>
    /// Data quality status (canonical): "good" | "warning" | "critical" | "insufficient_data" | null.
    /// Legacy values "fair"/"poor" may still exist in historical rows.
    /// </summary>
    public string? DataQualityStatus { get; set; }

    /// <summary>
    /// Action workflow status: "new" | "accepted" | "deferred" | "rejected" | "done"
    /// Maps inventory: pending->new, approved->accepted, deferred->deferred, closed->rejected/done
    /// </summary>
    public required string Status { get; set; }

    /// <summary>
    /// Optional URL to source entity or action page (e.g., /analytics/inventory?sku=123)
    /// </summary>
    public string? ActionUrl { get; set; }

    /// <summary>
    /// Optional JSON payload for source-specific metadata (doesn't lock schema)
    /// </summary>
    public string? MetadataJson { get; set; }

    /// <summary>
    /// When the action was created
    /// </summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the action was last updated
    /// </summary>
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the action was resolved (closed).
    /// Set only for terminal statuses: "rejected" or "done".
    /// Cleared if the action is reopened to "new", "accepted", or "deferred".
    /// </summary>
    public DateTime? ResolvedAtUtc { get; set; }

    /// <summary>
    /// User ID who created the action
    /// </summary>
    public string? CreatedByUserId { get; set; }

    /// <summary>
    /// User ID who last updated the action
    /// </summary>
    public string? UpdatedByUserId { get; set; }

    /// <summary>
    /// User name who last updated (for audit trail)
    /// </summary>
    public string? UpdatedByUserName { get; set; }
}
