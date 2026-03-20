using Application.Inventory.Models;

namespace Application.Common.Interfaces;

public interface IInventoryReportScheduleService
{
    Task<IReadOnlyList<InventoryReportScheduleDefinition>> ListAsync(CancellationToken ct = default);

    Task<IReadOnlyList<InventoryReportScheduleDefinition>> ListEnabledAsync(CancellationToken ct = default);

    Task<InventoryReportScheduleDefinition?> GetByIdAsync(long id, CancellationToken ct = default);

    Task<InventoryReportScheduleDefinition> UpsertAsync(long? id, InventoryReportScheduleUpsertRequest request, CancellationToken ct = default);

    Task MarkRunResultAsync(long id, InventoryReportScheduleRunResult result, CancellationToken ct = default);
}
