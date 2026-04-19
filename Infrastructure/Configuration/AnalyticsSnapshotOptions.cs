namespace Infrastructure.Configuration;

public sealed class AnalyticsSnapshotOptions
{
    public const string Section = "Analytics";

    public bool UseSnapshotCost { get; set; }
    public bool SnapshotAdminEnabled { get; set; }
    public int ActiveBatchStaleAfterHours { get; set; } = 72;
}
