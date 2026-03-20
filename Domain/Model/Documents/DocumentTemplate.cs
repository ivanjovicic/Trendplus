namespace Domain.Model.Documents;

public class DocumentTemplate
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Version { get; set; }
    public string Type { get; set; } = DocumentTemplateTypes.AnalyticsTableReport;
    public string Locale { get; set; } = "sr-RS";
    public string Content { get; set; } = string.Empty;
    public string? HeaderContent { get; set; }
    public string? FooterContent { get; set; }
    public bool IsActive { get; set; } = true;
    public string CreatedByUserId { get; set; } = "system";
    public DateTime CreatedAtUtc { get; set; }
}
