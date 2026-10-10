using Application.Analytics;
using Application.Artikli.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Analytics.Queries.GetInventoryStatus
{
    public class GetInventoryStatusHandler : IRequestHandler<GetInventoryStatusQuery, InventoryStatusDto>
    {
        private readonly IAnalyticsDbContext _db;
        private readonly ITrendplusDbContext _trendDb;

        public GetInventoryStatusHandler(IAnalyticsDbContext db, ITrendplusDbContext trendDb)
        {
            _db = db;
            _trendDb = trendDb;
        }

        public async Task<InventoryStatusDto> Handle(GetInventoryStatusQuery request, CancellationToken cancellationToken)
        {
            var query = _db.ProductsDim.AsNoTracking().AsQueryable();
            var normalizedDataScope = NormalizeDataScope(request.DataScope);

            // ProductsDim is the latest available stock snapshot; Timestamp marks
            // its refresh time, not stock history for a selected sales period.
            if (normalizedDataScope == "imported")
                query = query.Where(x => x.DataOrigin == "access");
            else if (normalizedDataScope == "existing")
                query = query.Where(x => x.DataOrigin == "existing" || x.DataOrigin == null || x.DataOrigin == "");

            if (request.StoreId.HasValue || request.SupplierId.HasValue)
            {
                var scopedArticleIds = await _trendDb.Artikli
                    .AsNoTracking()
                    .Where(x => (!request.StoreId.HasValue || x.IDObjekat == request.StoreId.Value)
                        && (!request.SupplierId.HasValue || x.IDDobavljac == request.SupplierId.Value))
                    .Select(x => x.Id)
                    .ToArrayAsync(cancellationToken);

                query = query.Where(x => scopedArticleIds.Contains(x.ProductId));
            }

            var result = await query.GroupBy(_ => 1).Select(g => new
            {
                TotalSkuCount = g.Count(),
                // Sum ignores null quantities; do not coalesce null → 0 into on-hand.
                TotalOnHand = g.Sum(x => x.Kolicina > 0 ? x.Kolicina : (int?)0) ?? 0,
                // Measured zero only — null quantity is not OOS.
                OutOfStockCount = g.Count(x => x.Kolicina == 0)
            }).FirstOrDefaultAsync(cancellationToken);

            var artikliQuery = _trendDb.Artikli.AsNoTracking().AsQueryable();
            // Artikli.UpdatedAt is the row refresh time, not historical stock.
            if (request.StoreId.HasValue)
                artikliQuery = artikliQuery.Where(x => x.IDObjekat == request.StoreId.Value);
            if (request.SupplierId.HasValue)
                artikliQuery = artikliQuery.Where(x => x.IDDobavljac == request.SupplierId.Value);
            if (normalizedDataScope == "imported")
                artikliQuery = artikliQuery.Where(x => x.DataOrigin == "access");
            else if (normalizedDataScope == "existing")
                artikliQuery = artikliQuery.Where(x => x.DataOrigin == "existing" || x.DataOrigin == null || x.DataOrigin == "");

            var lowStockPredicate = InventoryStockEvidence.MatchesLowStockSurface(request.LowStockThreshold);
            var lowStockCount = await artikliQuery.CountAsync(lowStockPredicate, cancellationToken);

            return new InventoryStatusDto(
                TotalSkuCount: result?.TotalSkuCount ?? 0,
                TotalOnHand: result?.TotalOnHand ?? 0,
                LowStockCount: lowStockCount,
                OutOfStockCount: result?.OutOfStockCount ?? 0
            );
        }

        private static string NormalizeDataScope(string? dataScope)
        {
            var normalized = (dataScope ?? "all").Trim().ToLowerInvariant();
            return normalized is "all" or "imported" or "existing" ? normalized : "all";
        }
    }
}
