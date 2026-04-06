namespace Application.Config;

public class PerformanceLoggingOptions
{
    public const string Section = "PerformanceLogging";

    public bool CaptureSql { get; set; } = false;
    public int MaxQueryLength { get; set; } = 2000;
    public double SampleRate { get; set; } = 1.0;
}
