namespace Application.Analytics.Queries;

/// <summary>
/// Makes the boundary between a requested Inventory period/scope and a current
/// snapshot explicit. The four cached secondary signal tables do not currently
/// carry authoritative period or data-origin dimensions.
/// </summary>
public sealed record InventorySignalSnapshotProvenance(
    string EvidenceScope,
    bool SupportsRequestedPeriod,
    bool SupportsRequestedDataScope,
    DateTime? RequestedPeriodFromUtc,
    DateTime? RequestedPeriodToUtc,
    string? RequestedDataScope,
    DateTime? EffectivePeriodFromUtc,
    DateTime? EffectivePeriodToUtc,
    string? EffectiveDataScope,
    string? Warning)
{
    public const string CurrentSnapshot = "current-snapshot";

    public static InventorySignalSnapshotProvenance ForCurrentSnapshot(
        DateTime? requestedPeriodFromUtc,
        DateTime? requestedPeriodToUtc,
        string? requestedDataScope) =>
        new(
            EvidenceScope: CurrentSnapshot,
            SupportsRequestedPeriod: false,
            SupportsRequestedDataScope: false,
            RequestedPeriodFromUtc: requestedPeriodFromUtc,
            RequestedPeriodToUtc: requestedPeriodToUtc,
            RequestedDataScope: requestedDataScope,
            EffectivePeriodFromUtc: null,
            EffectivePeriodToUtc: null,
            EffectiveDataScope: null,
            Warning: "Ovaj signal je trenutni snapshot; traženi period i data scope nisu primenjeni na izvorne redove.");
}
