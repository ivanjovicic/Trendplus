namespace Domain.Model.Analytics;

public class AnalyticsActionNote
{
    public long Id { get; set; }

    public long ActionItemId { get; set; }
    public AnalyticsActionItem ActionItem { get; set; } = null!;

    public string StatusFrom { get; set; } = string.Empty;
    public string StatusTo { get; set; } = string.Empty;

    public string? Note { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public string? CreatedByUserId { get; set; }
    public string? CreatedByUserName { get; set; }
}
