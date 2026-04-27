using System.ComponentModel.DataAnnotations;

namespace Domain.Model
{
    public sealed class DataImportBatch
    {
        [Key]
        public long Id { get; set; }

        public string SourceSystem { get; set; } = "access";
        public string SourceFileName { get; set; } = string.Empty;
        [MaxLength(800)]
        public string? SourceFilePath { get; set; }
        [MaxLength(1024)]
        public string? SourceStorageKey { get; set; }
        [MaxLength(32)]
        public string? SourceStorageProvider { get; set; }
        public DateTime QueuedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAtUtc { get; set; }
        public DateTime? LastHeartbeatUtc { get; set; }
        [MaxLength(32)]
        public string Status { get; set; } = "pending";
        public string? CurrentStep { get; set; }
        public string? CurrentTable { get; set; }
        public string? SummaryJson { get; set; }
        public string? ErrorMessage { get; set; }
        public string? ErrorDetailsJson { get; set; }
        [MaxLength(200)]
        public string? RequestedBy { get; set; }
        [MaxLength(16)]
        public string ImportMode { get; set; } = "auto";
        [MaxLength(32)]
        public string ImportStrategy { get; set; } = "full";
        public bool IncludeAnalytics { get; set; } = true;
        public bool OverwriteExisting { get; set; } = true;
        public bool IncludeTemporaryTables { get; set; }
        public bool SkipInvalidForeignKeys { get; set; } = true;
        public bool CancellationRequested { get; set; }
        public DateTime? CancellationRequestedAtUtc { get; set; }
        public int RetryCount { get; set; }
        public int ProgressPercent { get; set; }
        public int RowsRead { get; set; }
        public int RowsAccepted { get; set; }
        public int RowsWritten { get; set; }
        public bool IsIncremental { get; set; }
        public string? CursorSnapshot { get; set; }
        public string? CursorBeforeJson { get; set; }
        public string? CursorAfterJson { get; set; }
        public int ProcessedRowCount { get; set; }
        public int SkippedRowCount { get; set; }
        public int RowsInserted { get; set; }
        public int RowsUpdated { get; set; }
        public int RowsUnchanged { get; set; }
        public int RowsStaged { get; set; }
        public int RowsSkippedStale { get; set; }
        public int RowsRejected { get; set; }
        public int ShadowMismatchCount { get; set; }
        [MaxLength(128)]
        public string? SourceFileHash { get; set; }

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
