namespace Api.Config;

public sealed class AccessImportOptions
{
    public const string Section = "AccessImport";

    public int CliTimeoutSeconds { get; set; } = 60;
    public int PreviewSampleTake { get; set; } = 50;
    public int MaxMetadataParallelism { get; set; } = 2;
    public int DbSaveBatchSize { get; set; } = 1000;
    public bool EnableMdbSql { get; set; }
    public bool SkipInvalidForeignKeys { get; set; } = true;
    // If true, the importer will attempt to insert missing `prodaja_zaglavlje` rows
    // found in the Access file before importing `prodaja_stavke`. Default: false (opt-in).
    public bool AutoInsertMissingParents { get; set; } = false;
}
