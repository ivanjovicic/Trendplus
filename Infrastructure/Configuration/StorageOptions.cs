namespace Infrastructure.Configuration;

public sealed class StorageOptions
{
    public const string Section = "Storage";

    public string Provider { get; set; } = "local";
    public string LocalBasePath { get; set; } = "out/storage";
    public string Bucket { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string Region { get; set; } = "us-east-1";
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public bool UsePathStyle { get; set; } = true;
}
