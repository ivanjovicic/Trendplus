using Domain.Model;

namespace Api.Services.Access;

public interface IAccessImportCursorRepository
{
    Task<AccessImportCursor> GetOrCreateAsync(string sourceNamespace, string tableKey, string cursorMode, CancellationToken ct);
    Task<AccessImportCursor?> GetAsync(string sourceNamespace, string tableKey, CancellationToken ct);

    Task<bool> TryAcquireLeaseAsync(string sourceNamespace, string tableKey, string leaseOwner, TimeSpan leaseDuration, CancellationToken ct);
    Task<bool> RenewLeaseAsync(string sourceNamespace, string tableKey, string leaseOwner, TimeSpan leaseDuration, CancellationToken ct);
    Task ReleaseLeaseAsync(string sourceNamespace, string tableKey, string leaseOwner, CancellationToken ct);

    Task MarkRunStartedAsync(string sourceNamespace, string tableKey, CancellationToken ct);
    Task MarkRunCompletedAsync(string sourceNamespace, string tableKey, CancellationToken ct);

    Task CommitCursorAsync(
        string sourceNamespace,
        string tableKey,
        DateTime? cursorTimestampUtc,
        long? cursorId,
        long? cursorTieBreakerId,
        int rowsRead,
        int rowsMerged,
        int? lagSeconds,
        long? lastSuccessfulBatchId,
        CancellationToken ct);

    Task MarkFailureAsync(string sourceNamespace, string tableKey, string? error, CancellationToken ct);
}
