using Domain.Model;

namespace Application.Analytics;

/// <summary>
/// A chain-wide price event applies to every store; a store-scoped event applies
/// only to that store. This policy is shared by the analytics readers.
/// </summary>
public static class NivelacijaEventScopePolicy
{
    public const string VendorSqlStorePredicate =
        "(@storeId IS NULL OR d.\"IDObjekat\" IS NULL OR d.\"IDObjekat\" = @storeId::int)";

    public static IQueryable<DnevnikPromena> ApplyStoreScope(
        IQueryable<DnevnikPromena> query,
        int? storeId) =>
        query.Where(d => !storeId.HasValue || !d.IDObjekat.HasValue || d.IDObjekat == storeId.Value);

    public static bool AppliesToStore(int? eventStoreId, int? requestedStoreId) =>
        !requestedStoreId.HasValue || !eventStoreId.HasValue || eventStoreId == requestedStoreId.Value;
}
