using Application.Analytics.Queries.GetInventoryForecast;

namespace Application.Common.Interfaces;

public interface IInventoryForecastSnapshotMaterializerService
{
    Task<InventoryForecastSnapshotMaterializationResult> UpsertAsync(
        InventoryForecastSnapshotMaterializationRequest request,
        CancellationToken ct = default);

    Task<IReadOnlyList<InventoryForecastObservedPairDto>> ListObservedPairingsAsync(
        InventoryForecastObservedPairQuery request,
        CancellationToken ct = default);
}
