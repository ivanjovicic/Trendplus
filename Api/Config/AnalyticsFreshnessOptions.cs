namespace Api.Config;

public sealed class AnalyticsFreshnessOptions
{
    public const string Section = "AnalyticsFreshness";

    /// <summary>
    /// Age after which durable Access import evidence is shown as a warning.
    /// </summary>
    public int WarningAfterHours { get; set; } = 48;

    /// <summary>
    /// Age after which durable Access import evidence is shown as critical.
    /// </summary>
    public int CriticalAfterHours { get; set; } = 168;

    public AnalyticsFreshnessNotificationOptions Notifications { get; set; } = new();

    public void Validate()
    {
        if (WarningAfterHours < 1)
        {
            throw new InvalidOperationException($"{Section}:WarningAfterHours must be at least 1.");
        }

        if (CriticalAfterHours <= WarningAfterHours)
        {
            throw new InvalidOperationException($"{Section}:CriticalAfterHours must be greater than WarningAfterHours.");
        }

        if (Notifications.MinimumIntervalHours < 1)
        {
            throw new InvalidOperationException($"{Section}:Notifications:MinimumIntervalHours must be at least 1.");
        }
    }
}

public sealed class AnalyticsFreshnessNotificationOptions
{
    /// <summary>
    /// Notifications are opt-in. No message is sent with the default configuration.
    /// </summary>
    public bool Enabled { get; set; }

    public int MinimumIntervalHours { get; set; } = 24;

    public string? Recipient { get; set; }
}
