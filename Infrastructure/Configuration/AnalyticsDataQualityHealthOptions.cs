namespace Infrastructure.Configuration;

public sealed class AnalyticsDataQualityHealthOptions
{
    public const string Section = "AnalyticsDataQualityHealth";

    public bool Enabled { get; set; } = true;
    public int StartupDelaySeconds { get; set; } = 45;
    public int PauseCheckSeconds { get; set; } = 30;
    public int PollIntervalMinutes { get; set; } = 60;
    public int LookbackDays { get; set; } = 90;
    public int WarningOrphanArticleCount { get; set; } = 10;
    public double WarningUnknownSupplierRevenueSharePct { get; set; } = 3;
    public double WarningMissingCostRevenueSharePct { get; set; } = 5;
    public decimal MinSalesForNoisyIssuesRsd { get; set; } = 1000m;
    public int TopOffenderLimit { get; set; } = 10;
    public double ScoreMissingCostWeight { get; set; } = 0.5d;
    public double ScoreUnknownSupplierWeight { get; set; } = 0.3d;
    public double ScoreOrphanWeight { get; set; } = 0.2d;
    public double ScorePenaltyAtWarning { get; set; } = 0.45d;
    public double ScoreCriticalMultiplier { get; set; } = 3d;
}
