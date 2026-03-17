namespace Infrastructure.Configuration;

/// <summary>
/// Configuration for TrendIngestionWorker — controls Python API URL, schedule, and limits.
/// Bind from appsettings section "TrendIngestion".
/// </summary>
public sealed class TrendIngestionOptions
{
    public const string Section = "TrendIngestion";

    /// <summary>Base URL of the consolidated Python API service (default: http://localhost:8000).</summary>
    public string PythonApiBaseUrl { get; set; } = "http://localhost:8000";

    /// <summary>Number of pages to scrape per source (passed to Python API).</summary>
    public int Pages { get; set; } = 5;

    /// <summary>Markets to scrape, comma-separated or as JSON array (passed to Python API).</summary>
    public List<string> Markets { get; set; } = ["DE", "AT"];

    /// <summary>Maximum number of trend results to ingest per run.</summary>
    public int Top { get; set; } = 500;

    /// <summary>UTC hour at which the daily ingestion runs (0–23).</summary>
    public int RunAtHourUtc { get; set; } = 3;

    /// <summary>Delay in minutes to wait after application start before the worker becomes active.</summary>
    /// Useful for postponing heavy scrapes on startup (0 = no delay).
    public int StartDelayMinutes { get; set; } = 0;
    /// <summary>Timeout in seconds for the HTTP call to the Python API.</summary>
    public int PythonCallTimeoutSeconds { get; set; } = 300;

    /// <summary>When true the worker is enabled regardless of the global Workers switch.</summary>
    public bool Enabled { get; set; } = true;
}
