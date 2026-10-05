using Application.Artikli.Common.Interfaces;
using Application.Common.Interfaces;
using Domain.Model;
using Microsoft.EntityFrameworkCore;

namespace Application.Analytics;

public static class InventoryValuationSupport
{
    private const int BatchSize = 2_000;

    public sealed record MovementCostRow(
        int ArtikalId,
        string TipPromene,
        DateTime Datum,
        decimal Iznos,
        int? Kolicina,
        string DataOrigin);

    public static async Task<Dictionary<int, decimal>> LoadLatestSaleLineUnitCostsAsync(
        ITrendplusDbContext db,
        IReadOnlyCollection<int> articleIds,
        int? storeId,
        CancellationToken ct)
    {
        var result = new Dictionary<int, decimal>();
        if (articleIds.Count == 0)
        {
            return result;
        }

        foreach (var batch in articleIds.Distinct().Chunk(BatchSize))
        {
            var rows = await (
                from ps in db.ProdajaStavke.AsNoTracking()
                join pz in db.ProdajaZaglavlja.AsNoTracking() on ps.IdProdaja equals pz.Id
                where batch.Contains(ps.IdArtikal)
                      && ps.NabavnaCena.HasValue
                      && ps.NabavnaCena > 0m
                      && (!storeId.HasValue || pz.IDObjekat == storeId.Value)
                group new { ps.IdArtikal, ps.NabavnaCena, pz.DatumProdaje } by ps.IdArtikal
                into g
                select new
                {
                    ArtikalId = g.Key,
                    UnitCost = g.OrderByDescending(x => x.DatumProdaje).Select(x => x.NabavnaCena).First()
                })
                .ToListAsync(ct);

            foreach (var row in rows)
            {
                if (row.UnitCost is > 0m)
                {
                    result[row.ArtikalId] = row.UnitCost.Value;
                }
            }
        }

        return result;
    }

    public static async Task<Dictionary<int, (decimal UnitCost, DateTime Datum)>> LoadLatestInboundReceiptsAsync(
        IAnalyticsDbContext analyticsDb,
        ITrendplusDbContext trendDb,
        IReadOnlyCollection<int> articleIds,
        int? storeId,
        CancellationToken ct)
    {
        var result = new Dictionary<int, (decimal UnitCost, DateTime Datum)>();
        if (articleIds.Count == 0)
        {
            return result;
        }

        foreach (var batch in articleIds.Distinct().Chunk(BatchSize))
        {
            var analyticsRows = await analyticsDb.InventoryMovementFacts
                .AsNoTracking()
                .Where(x => x.ArtikalId.HasValue
                    && batch.Contains(x.ArtikalId.Value)
                    && (!storeId.HasValue || x.StoreId == storeId.Value))
                .Select(x => new MovementCostRow(
                    x.ArtikalId!.Value,
                    x.TipPromene,
                    x.Datum,
                    x.Iznos,
                    x.Kolicina,
                    x.DataOrigin))
                .ToListAsync(ct);

            var trendRows = await trendDb.DnevnikPromena
                .AsNoTracking()
                .Where(x => x.ArtikalId.HasValue
                    && batch.Contains(x.ArtikalId.Value)
                    && (!storeId.HasValue || x.IDObjekat == storeId.Value))
                .Select(x => new MovementCostRow(
                    x.ArtikalId!.Value,
                    x.TipPromene,
                    x.Datum,
                    x.Iznos,
                    x.Kolicina,
                    x.DataOrigin ?? "existing"))
                .ToListAsync(ct);

            foreach (var row in analyticsRows.Concat(trendRows))
            {
                if (InventoryValuationAndAgingPolicy.IsSyntheticImportReceipt(
                        row.TipPromene,
                        row.Iznos,
                        row.Kolicina,
                        row.DataOrigin))
                {
                    continue;
                }

                if (!InventoryValuationAndAgingPolicy.IsReliableInboundMovement(
                        row.TipPromene,
                        row.Iznos,
                        row.Kolicina))
                {
                    continue;
                }

                var unitCost = InventoryValuationAndAgingPolicy.ResolveInboundUnitCost(row.Iznos, row.Kolicina);
                if (unitCost is null or <= 0m)
                {
                    continue;
                }

                if (!result.TryGetValue(row.ArtikalId, out var existing) || row.Datum > existing.Datum)
                {
                    result[row.ArtikalId] = (unitCost.Value, row.Datum);
                }
            }
        }

        return result;
    }

    public static async Task<Dictionary<int, DateTime?>> LoadLastRealReceiptDatesAsync(
        IAnalyticsDbContext analyticsDb,
        ITrendplusDbContext trendDb,
        IReadOnlyCollection<int> articleIds,
        int? storeId,
        CancellationToken ct)
    {
        var inbound = await LoadLatestInboundReceiptsAsync(analyticsDb, trendDb, articleIds, storeId, ct);
        return articleIds.ToDictionary(
            id => id,
            id => inbound.TryGetValue(id, out var receipt) ? (DateTime?)receipt.Datum : null);
    }

    public static async Task<InventoryValuationAggregate> BuildValuationAggregateAsync(
        ITrendplusDbContext trendDb,
        IAnalyticsDbContext analyticsDb,
        IReadOnlyCollection<(int ArticleId, int? Quantity)> rows,
        int? storeId,
        CancellationToken ct)
    {
        var articleIds = rows.Select(row => row.ArticleId).Distinct().ToArray();
        var inboundCosts = await LoadLatestInboundReceiptsAsync(analyticsDb, trendDb, articleIds, storeId, ct);
        var saleCosts = await LoadLatestSaleLineUnitCostsAsync(trendDb, articleIds, storeId, ct);

        return InventoryValuationAndAgingPolicy.AggregateValuation(
            rows.Select(row => (row.Quantity, ResolveStoredArticleValuation(row.ArticleId, row.Quantity, inboundCosts, saleCosts))));
    }

    public static async Task<Dictionary<int, InventoryArticleValuation>> LoadArticleValuationsAsync(
        ITrendplusDbContext trendDb,
        IAnalyticsDbContext analyticsDb,
        IReadOnlyCollection<int> articleIds,
        int? storeId,
        CancellationToken ct)
    {
        if (articleIds.Count == 0)
        {
            return new Dictionary<int, InventoryArticleValuation>();
        }

        var inboundCosts = await LoadLatestInboundReceiptsAsync(analyticsDb, trendDb, articleIds, storeId, ct);
        var saleCosts = await LoadLatestSaleLineUnitCostsAsync(trendDb, articleIds, storeId, ct);

        return articleIds
            .Distinct()
            .ToDictionary(
                id => id,
                id => ResolveStoredArticleValuation(id, quantity: null, inboundCosts, saleCosts));
    }

    private static InventoryArticleValuation ResolveStoredArticleValuation(
        int articleId,
        int? quantity,
        IReadOnlyDictionary<int, (decimal UnitCost, DateTime Datum)> inboundCosts,
        IReadOnlyDictionary<int, decimal> saleCosts)
    {
        decimal? inboundUnitCost = inboundCosts.TryGetValue(articleId, out var inbound) ? inbound.UnitCost : null;
        decimal? saleUnitCost = saleCosts.TryGetValue(articleId, out var saleCost) ? saleCost : null;
        return InventoryValuationAndAgingPolicy.ResolveArticleValuation(quantity, inboundUnitCost, saleUnitCost);
    }
}
