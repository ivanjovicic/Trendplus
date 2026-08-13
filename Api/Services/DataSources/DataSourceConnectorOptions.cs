namespace Api.Services.DataSources;

public sealed class DataSourceConnectorOptions
{
    public const string SectionName = "DataSources";

    public Dictionary<string, DataSourceProfileOptions> Sources { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class DataSourceProfileOptions
{
    public string Provider { get; set; } = string.Empty;

    public string? DisplayName { get; set; }

    /// <summary>Never returned from discovery APIs. Prefer environment variables over committed files.</summary>
    public string? ConnectionString { get; set; }
}
