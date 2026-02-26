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

        // ── Enhanced columns (migration 015) ──
        public int? DurationSeconds { get; set; }
        public int TotalImported { get; set; }
        public int TotalUpdated { get; set; }
        public int TotalErrors { get; set; }

        [MaxLength(32)]
        public string DataOrigin { get; set; } = "access";

        // Navigation
        public ICollection<AccessImportLog> LogEntries { get; set; } = new List<AccessImportLog>();
    }
}
