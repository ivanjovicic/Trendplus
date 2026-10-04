using Domain.Model;
using Application.Analytics;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace Trendplus2.Endpoints;

internal static class SupplierSalesStatsQuerySupport
{
    internal static Task<Dictionary<int, DateTime>> LoadFirstNivelacijaByArticleAsync(
        TrendplusDbContext db,
        int[] relevantArticleIds,
        DateTime? toUtc,
        int? storeId,
        CancellationToken cancellationToken)
    {
        if (relevantArticleIds.Length == 0)
        {
            return Task.FromResult(new Dictionary<int, DateTime>());
        }

        var events = db.DnevnikPromena.AsNoTracking()
            .Where(d =>
                (d.TipPromene == TipPromeneConstants.Nivelacija || d.TipPromene == TipPromeneConstants.NivelacijaCena) &&
                d.ArtikalId.HasValue &&
                relevantArticleIds.Contains(d.ArtikalId.Value) &&
                (!toUtc.HasValue || d.Datum < toUtc.Value));

        return NivelacijaEventScopePolicy.ApplyStoreScope(events, storeId)
            .GroupBy(d => d.ArtikalId!.Value)
            .Select(g => new
            {
                ArtikalId = g.Key,
                PrvaDatum = g.Min(x => x.Datum)
            })
            .ToDictionaryAsync(x => x.ArtikalId, x => x.PrvaDatum, cancellationToken);
    }

    internal static Task<Dictionary<int, DateTime>> LoadLatestNivelacijaByArticleAsync(
        TrendplusDbContext db,
        int[] relevantArticleIds,
        DateTime? toUtc,
        int? storeId,
        CancellationToken cancellationToken)
    {
        if (relevantArticleIds.Length == 0)
        {
            return Task.FromResult(new Dictionary<int, DateTime>());
        }

        var events = db.DnevnikPromena.AsNoTracking()
            .Where(d =>
                (d.TipPromene == TipPromeneConstants.Nivelacija || d.TipPromene == TipPromeneConstants.NivelacijaCena) &&
                d.ArtikalId.HasValue &&
                relevantArticleIds.Contains(d.ArtikalId.Value) &&
                (!toUtc.HasValue || d.Datum < toUtc.Value));

        return NivelacijaEventScopePolicy.ApplyStoreScope(events, storeId)
            .GroupBy(d => d.ArtikalId!.Value)
            .Select(g => new
            {
                ArtikalId = g.Key,
                PoslednjaDatum = g.Max(x => x.Datum)
            })
            .ToDictionaryAsync(x => x.ArtikalId, x => x.PoslednjaDatum, cancellationToken);
    }

    internal static Task<Dictionary<int, decimal>> LoadSnapshotCostsForSaleLinesAsync(
        TrendplusDbContext db,
        long activeBatchId,
        int[] saleLineIds,
        CancellationToken cancellationToken)
    {
        if (saleLineIds.Length == 0)
        {
            return Task.FromResult(new Dictionary<int, decimal>());
        }

        return db.AnalyticsSaleLineCostSnapshots.AsNoTracking()
            .Where(snapshot => snapshot.BatchId == activeBatchId && saleLineIds.Contains(snapshot.ProdajaStavkaId))
            .ToDictionaryAsync(snapshot => snapshot.ProdajaStavkaId, snapshot => snapshot.ResolvedUnitCost, cancellationToken);
    }
}
