namespace Infrastructure.Configuration;

public sealed class OperationsAnalyticsIntegrityOptions
{
    public const string Section = "OperationsAnalyticsIntegrity";

    public bool Enabled { get; set; } = true;
    public int StartupDelaySeconds { get; set; } = 60;
    public int PauseCheckSeconds { get; set; } = 30;
    public int PollIntervalMinutes { get; set; } = 30;
    public int ProbeLookbackDays { get; set; } = 7;
    public string DefaultDataScope { get; set; } = "all";
    public decimal RevenueToleranceRsd { get; set; } = 0.01m;
}
