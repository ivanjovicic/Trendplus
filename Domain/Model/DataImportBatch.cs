using System.ComponentModel.DataAnnotations;

namespace Domain.Model
{
    public sealed class DataImportBatch
    {
        [Key]
        public long Id { get; set; }

        public string SourceSystem { get; set; } = "access";
        public string SourceFileName { get; set; } = string.Empty;
        public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAtUtc { get; set; }
        public string Status { get; set; } = "running";
        public string? SummaryJson { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
