using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Model;

/// <summary>Row-level import log entry for observability / debugging.</summary>
public sealed class AccessImportLog
{
    [Key]
    public long Id { get; set; }

    public long BatchId { get; set; }

    [ForeignKey(nameof(BatchId))]
    public DataImportBatch? Batch { get; set; }

    [MaxLength(128)]
    public string TableName { get; set; } = string.Empty;

    public int RowIndex { get; set; }

    /// <summary>info | warning | error</summary>
    [MaxLength(16)]
    public string Severity { get; set; } = "info";

    [MaxLength(2000)]
    public string Message { get; set; } = string.Empty;

    /// <summary>Optional JSON of the source row for error diagnosis.</summary>
    public string? SourceRowJson { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
