using Domain.Model;

namespace Api.Services.Access;

public interface IAccessImportCursorRepository
{
    Task<AccessImportCursor> GetOrCreateAsync(string tableKey, string cursorMode, CancellationToken ct);
    Task<AccessImportCursor?> GetAsync(string tableKey, CancellationToken ct);

    Task<bool> TryAcquireLeaseAsync(string tableKey, string leaseOwner, TimeSpan leaseDuration, CancellationToken ct);
    Task<bool> RenewLeaseAsync(string tableKey, string leaseOwner, TimeSpan leaseDuration, CancellationToken ct);
    Task ReleaseLeaseAsync(string tableKey, string leaseOwner, CancellationToken ct);

    Task MarkRunStartedAsync(string tableKey, CancellationToken ct);
    Task MarkRunCompletedAsync(string tableKey, CancellationToken ct);

    Task CommitCursorAsync(
        string tableKey,
        DateTime? cursorTimestampUtc,
        long? cursorId,
        long? cursorTieBreakerId,
        int rowsRead,
        int rowsMerged,
        int? lagSeconds,
        long? lastSuccessfulBatchId,
        CancellationToken ct);

    Task MarkFailureAsync(string tableKey, string? error, CancellationToken ct);
}
