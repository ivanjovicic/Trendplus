namespace Infrastructure.Configuration;

public sealed class AnalyticsDataQualityHealthOptions
{
    public const string Section = "AnalyticsDataQualityHealth";

    public bool Enabled { get; set; } = true;
    public int StartupDelaySeconds { get; set; } = 45;
    public int PauseCheckSeconds { get; set; } = 30;
    public int PollIntervalMinutes { get; set; } = 60;
    public int LookbackDays { get; set; } = 90;
    public int WarningOrphanArticleCount { get; set; } = 1;
    public double WarningUnknownSupplierRevenueSharePct { get; set; } = 1;
    public double WarningMissingCostRevenueSharePct { get; set; } = 5;
}
