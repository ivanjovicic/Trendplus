namespace Infrastructure.Configuration;

public sealed class SmtpOptions
{
    public const string Section = "Smtp";

    public bool Enabled { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string FromAddress { get; set; } = "reports@trendplus.local";
    public string? FromName { get; set; } = "Trendplus Reports";
    public int AttachmentSizeLimitMb { get; set; } = 10;
}
