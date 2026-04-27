namespace Domain.Model;

public interface IAccessImportSourceLineage
{
    string? SourceTableKey { get; set; }
    long? SourceRowId { get; set; }
    DateTime? SourceUpdatedAtUtc { get; set; }
    string? SourceHash { get; set; }
    long? SourceBatchId { get; set; }
}
