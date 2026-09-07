namespace Application.Analytics.Queries;

/// <summary>
/// Shared freshness vocabulary for inventory signal snapshots.
/// The current alert, rebalance and size-curve read paths have no proven
/// materializer lineage, so their handlers must fail closed as unknown.
/// </summary>
public static class InventorySnapshotFreshness
{
    public const string Unknown = "unknown";
}
