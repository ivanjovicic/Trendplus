namespace Api.Config;

public sealed class DataSourceOptions
{
    public const string Section = "DataSources";

    public int ConnectionTestTimeoutSeconds { get; set; } = 10;
    public int DiscoveryTimeoutSeconds { get; set; } = 20;
    public int PreviewTimeoutSeconds { get; set; } = 30;
    public int PreviewSampleLimit { get; set; } = 25;
    public List<NamedDataSourceProfileOptions> Profiles { get; set; } = [];
}

public sealed class NamedDataSourceProfileOptions
{
    public string Name { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string? Mode { get; set; }
    public bool Enabled { get; set; } = true;
    public string? ConnectionString { get; set; }
    public string? FilePath { get; set; }
    public string? DefaultSchema { get; set; }
    public string? Description { get; set; }
    public int? CommandTimeoutSeconds { get; set; }
}
