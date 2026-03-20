namespace Infrastructure.Configuration;

public sealed class DocumentExportOptions
{
    public const string Section = "Documents";

    public int SyncRowLimit { get; set; } = 5000;
    public int WorkerBatchSize { get; set; } = 4;
    public int SignedUrlTtlMinutes { get; set; } = 30;
    public int FileTtlHours { get; set; } = 72;
    public string StorageRoot { get; set; } = "out/documents";
    public string CsvDelimiter { get; set; } = "comma";
    public string? SigningKey { get; set; }

    public string ResolveSigningKey()
    {
        return Environment.GetEnvironmentVariable("DOCUMENT_SIGNING_KEY")
            ?? SigningKey
            ?? string.Empty;
    }
}
