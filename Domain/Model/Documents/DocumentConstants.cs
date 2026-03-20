namespace Domain.Model.Documents;

public static class DocumentFormats
{
    public const string Csv = "csv";
    public const string Xlsx = "xlsx";
    public const string Pdf = "pdf";
    public const string Html = "html";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        Csv,
        Xlsx,
        Pdf,
        Html
    };
}

public static class DocumentStatuses
{
    public const string Requested = "requested";
    public const string Queued = "queued";
    public const string Processing = "processing";
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string Poisoned = "poisoned";
    public const string Canceled = "canceled";
}

public static class DocumentOrientations
{
    public const string Portrait = "portrait";
    public const string Landscape = "landscape";
}

public static class DocumentTemplateTypes
{
    public const string AnalyticsTableReport = "analytics-table-report";
    public const string ExecutiveSummary = "executive-summary";
    public const string Receipt = "receipt";
    public const string Label = "label";
}

public static class DocumentAuditActions
{
    public const string Requested = "requested";
    public const string Queued = "queued";
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string Downloaded = "downloaded";
    public const string Printed = "printed";
    public const string Previewed = "previewed";
}
