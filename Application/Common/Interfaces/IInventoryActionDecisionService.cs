using Application.Inventory.Models;

namespace Application.Common.Interfaces;

public interface IInventoryActionDecisionService
{
    Task<IReadOnlyDictionary<string, InventoryActionDecisionDefinition>> ListAsync(CancellationToken ct = default);

    Task<InventoryActionDecisionDefinition> UpsertAsync(InventoryActionDecisionUpsertRequest request, CancellationToken ct = default);
}
